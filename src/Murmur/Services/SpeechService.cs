using Murmur.Core;
using Whisper.net;
using NAudio.Wave;
using System.Diagnostics;

namespace Murmur.Services;

public sealed class SpeechService(Store store, ModelManager models) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private FastSpeechEngine? fast;
    private readonly DecodedAudioCache decoded = new();
    public void ClearPreparedAudio() => decoded.Clear();
    private WhisperVadFactory? vadFactory;
    private WhisperVadProcessor? vad;
    private DateTime lastUsed;
    public async Task Prepare(CancellationToken ct, string? model = null)
    {
        try
        {
            await gate.WaitAsync(ct);
            try { if ((model ?? store.Settings.SpeechModel) == ModelManager.FastEnglish) await EnsureFast(ct); await EnsureVad(ct); }
            finally { lastUsed = DateTime.UtcNow; gate.Release(); }
        }
        catch (Exception) { /* Transcribe reports and preserves recoverable audio. */ }
    }
    private async Task EnsureFast(CancellationToken ct)
    {
        await models.Verify(ModelManager.FastEnglish, ct);
        if (fast is not null) return;
        var loaded = await Task.Run(() => new FastSpeechEngine(models), ct);
        if (ct.IsCancellationRequested) { loaded.Dispose(); ct.ThrowIfCancellationRequested(); }
        fast = loaded;
    }
    private async Task EnsureVad(CancellationToken ct)
    {
        await models.Verify("ggml-silero-v6.2.0.bin", ct);
        if (vad is not null) return;
        await Task.Run(() =>
        {
            vadFactory = WhisperVadFactory.FromPath(models.PathFor("ggml-silero-v6.2.0.bin"));
            vad = vadFactory.CreateBuilder().WithUseGpu(false).WithThreads(1).WithThreshold(.35f)
                .WithMinSpeechDuration(TimeSpan.FromMilliseconds(80)).WithMinSilenceDuration(TimeSpan.FromMilliseconds(450))
                .WithSpeechPadding(TimeSpan.FromMilliseconds(300)).Build();
        }, ct);
        ct.ThrowIfCancellationRequested();
    }
    private void UnloadCore() { decoded.Clear(); fast?.Dispose(); fast = null; vad?.Dispose(); vad = null; vadFactory?.Dispose(); vadFactory = null; }
    public void ReleaseIfIdle()
    {
        if (DateTime.UtcNow - lastUsed < TimeSpan.FromSeconds(30) || !gate.Wait(0)) return;
        try { UnloadCore(); } finally { gate.Release(); }
    }
    public void Unload()
    {
        if (!gate.Wait(0)) throw new InvalidOperationException("Wait for speech processing to finish first.");
        try { UnloadCore(); } finally { gate.Release(); }
    }
    public async Task<(List<Segment> Segments, double Milliseconds)> Transcribe(string audio, string speaker, CancellationToken ct, string? model = null)
    {
        var settings = model is null ? store.Settings : store.Settings with { SpeechModel = model };
        await gate.WaitAsync(ct);
        var watch = Stopwatch.StartNew();
        var mono = Path.Combine(store.Recordings, Guid.NewGuid() + ".16k.wav");
        try
        {
            await models.Verify(settings.SpeechModel, ct);
            await EnsureVad(ct);
            if (settings.SpeechModel == ModelManager.FastEnglish) await EnsureFast(ct);
            await Task.Run(() => AudioService.ToMono16(audio, mono, ct), ct);
            var result = await Task.Run(async () =>
            {
                using var reader = new AudioFileReader(mono);
                // At most 30 seconds of float PCM is held, irrespective of meeting length.
                var buffer = new float[16000 * 30]; long offset = 0;
                var segments = new List<Segment>();
                WhisperFactory? factory = null; WhisperProcessor? processor = null;
                try
                {
                    int carried = 0;
                    while (true)
                    {
                        int n = carried + reader.Read(buffer, carried, buffer.Length - carried);
                        if (n == 0) break;
                        ct.ThrowIfCancellationRequested();
                        var samples = buffer.AsMemory(0, n);
                        var detected = await vad!.DetectSpeechAsync(samples, ct);
                        ct.ThrowIfCancellationRequested();
                        int available = n;
                        if (n == buffer.Length)
                        {
                            // Move a full-window boundary into a natural pause when one
                            // exists, retaining the unused PCM for the following window.
                            double previousEnd = 0, boundary = 0;
                            foreach (var part in detected)
                            {
                                if (part.Start.TotalSeconds - previousEnd >= .15 && previousEnd >= 15 && part.Start.TotalSeconds <= 29) boundary = (previousEnd + part.Start.TotalSeconds) / 2;
                                previousEnd = part.End.TotalSeconds;
                            }
                            if (boundary > 0) { n = (int)(boundary * 16000); samples = buffer.AsMemory(0, n); }
                        }
                        void Advance() { offset += n; carried = available - n; if (carried > 0) Array.Copy(buffer, n, buffer, 0, carried); }
                        if (detected.Count > 0)
                        {
                            if (settings.SpeechModel == ModelManager.FastEnglish)
                            {
                                // Keep natural utterances together while giving meeting
                                // playback and optional speaker labels useful timestamps.
                                var ranges = new List<(int Start, int End)>();
                                // For a short dictation, VAD gates silence but
                                // does not trim the first/last words from ASR.
                                if (n <= 16000 * 20) ranges.Add((0, n));
                                else
                                foreach (var part in detected)
                                {
                                    int begin = Math.Clamp((int)(part.Start.TotalSeconds * 16000), 0, n);
                                    int end = Math.Clamp((int)(part.End.TotalSeconds * 16000), begin, n);
                                    if (end == begin) continue;
                                    if (ranges.Count > 0 && begin - ranges[^1].End < 12800 && end - ranges[^1].Start < 400000) ranges[^1] = (ranges[^1].Start, end);
                                    else ranges.Add((begin, end));
                                }
                                foreach (var range in ranges)
                                {
                                    var utterance = buffer.AsSpan(range.Start, range.End - range.Start).ToArray();
                                    var key = DecodedAudioCache.Key(settings.SpeechModel, utterance);
                                    if (!decoded.TryGet(key, out var value))
                                    {
                                        value = fast!.Decode(utterance, utterance.Length, ct);
                                        ct.ThrowIfCancellationRequested();
                                        decoded.Add(key, value);
                                    }
                                    if (value.Length > 0) segments.Add(new((offset + range.Start) / 16000d, (offset + range.End) / 16000d, value, speaker));
                                }
                                Advance();
                                continue;
                            }
                            if (processor is null)
                            {
                                factory = WhisperFactory.FromPath(models.PathFor(settings.SpeechModel));
                                var builder = factory.CreateBuilder().WithLanguage("en").WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 2, 6))
                                    .WithNoContext().WithTemperature(0).WithNoSpeechThreshold(.5f).WithProbabilities();
                                var prompt = string.Join(", ", store.List<LibraryEntry>("dictionary").Select(x => x.Value).Take(80));
                                if (prompt.Length > 0) builder.WithPrompt(prompt);
                                processor = builder.Build();
                            }
                            await foreach (var item in processor.ProcessAsync(samples, ct))
                            {
                                ct.ThrowIfCancellationRequested();
                                var value = item.Text.Trim();
                                if (value.Length > 0 && !value.StartsWith("[BLANK_AUDIO]", StringComparison.Ordinal) && !value.StartsWith("[ Silence", StringComparison.OrdinalIgnoreCase))
                                    segments.Add(new(offset / 16000d + item.Start.TotalSeconds, offset / 16000d + Math.Min(n / 16000d, item.End.TotalSeconds), value, speaker));
                            }
                        }
                        Advance();
                    }
                }
                finally { if (processor is not null) await processor.DisposeAsync(); factory?.Dispose(); }
                return segments;
            }, ct);
            ct.ThrowIfCancellationRequested();
            return (result, watch.Elapsed.TotalMilliseconds);
        }
        finally { try { File.Delete(mono); } finally { lastUsed = DateTime.UtcNow; gate.Release(); } }
    }
    public void Dispose() { if (gate.Wait(0)) { try { UnloadCore(); } finally { gate.Release(); } } }
}
