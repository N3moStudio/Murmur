using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Murmur.Core;
using NAudio.Wave;
using SherpaOnnx;

namespace Murmur.Services;

public sealed record SpeakerTurn(double Start, double End, int Speaker, double Confidence);
public sealed class SpeakerService(Store store, ModelManager models)
{
    public const string Segmentation = "speaker-segmentation.int8.onnx", Embedding = "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx";
    public async Task<List<Segment>> Label(string audio, List<Segment> segments, string source, CancellationToken ct)
    {
        await models.Verify(Segmentation, ct); await models.Verify(Embedding, ct);
        string result = Path.Combine(store.Recordings, Guid.NewGuid() + ".speakers.json");
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(typeof(SpeakerService).Assembly.Location);
        foreach (var a in new[] { "--diarize", audio, "--data-dir", store.Root, "--output", result }) info.ArgumentList.Add(a);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start speaker identification.");
        process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { }; process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var cancel = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        try
        {
            await process.WaitForExitAsync(ct); ct.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(result)) throw new InvalidOperationException("Speaker identification did not finish. Your transcript and source labels are kept.");
            var turns = Json.Read<List<SpeakerTurn>>(await File.ReadAllTextAsync(result, ct));
            return segments.Select(segment =>
            {
                var overlaps = turns.Select(t => new { Turn = t, Seconds = Math.Max(0, Math.Min(segment.End, t.End) - Math.Max(segment.Start, t.Start)) }).Where(t => t.Seconds > 0).OrderByDescending(t => t.Seconds).ToList();
                if (overlaps.Count == 0) return segment;
                var primary = overlaps[0].Turn; bool uncertain = overlaps.Skip(1).Any(t => t.Turn.Speaker != primary.Speaker && t.Seconds > overlaps[0].Seconds * .45);
                return segment with { Speaker = $"{source} · Speaker {primary.Speaker + 1}" + (uncertain ? " (mixed turn)" : "") };
            }).ToList();
        }
        finally { if (!process.HasExited) { try { process.Kill(true); process.WaitForExit(3000); } catch (InvalidOperationException) { } } File.Delete(result); File.Delete(result + ".wav"); }
    }
    public static List<SpeakerTurn> RunWorker(Store store, ModelManager models, string audio, string? workingFile = null)
    {
        models.Verify(Segmentation, CancellationToken.None).GetAwaiter().GetResult(); models.Verify(Embedding, CancellationToken.None).GetAwaiter().GetResult();
        var mono = workingFile ?? Path.Combine(store.Recordings, Guid.NewGuid() + ".speaker-audio.wav");
        try
        {
            AudioService.ToMono16(audio, mono);
            using var reader = new AudioFileReader(mono);
            if (reader.TotalTime > TimeSpan.FromHours(3)) throw new InvalidOperationException("Speaker identification supports recordings up to 3 hours.");
            // This worker exits after one run, releasing all large native buffers.
            var samples = new float[checked((int)(reader.Length / sizeof(float)))]; int received = 0, n;
            while ((n = reader.Read(samples, received, samples.Length - received)) > 0) received += n;
            if (received != samples.Length) Array.Resize(ref samples, received);
            var config = new OfflineSpeakerDiarizationConfig();
            config.Segmentation.Pyannote.Model = models.PathFor(Segmentation); config.Segmentation.NumThreads = 2;
            config.Embedding.Model = models.PathFor(Embedding); config.Embedding.NumThreads = 2;
            config.Clustering.ComputeConfidence = 1;
            using var native = DiarizationHandle.Create(config);
            if (Native.SampleRate(native) != 16000) throw new InvalidOperationException("Unsupported speaker model sample rate.");
            nint result = Native.Process(native, samples, samples.Length); if (result == 0) throw new InvalidOperationException("Speaker identification returned no result.");
            nint ordered = 0;
            try
            {
                int count = Native.Count(result); if (count is < 0 or > 1_000_000) throw new InvalidDataException("Invalid speaker result size.");
                ordered = Native.Sort(result); if (count > 0 && ordered == 0) throw new InvalidDataException("Missing speaker result.");
                var turns = new List<SpeakerTurn>(count);
                for (int i = 0; i < count; i++) { var turn = Marshal.PtrToStructure<NativeTurn>(ordered + i * Marshal.SizeOf<NativeTurn>()); turns.Add(new(turn.Start, turn.End, turn.Speaker, turn.Confidence)); }
                return turns;
            }
            finally { if (ordered != 0) Native.DestroySegments(ordered); Native.DestroyResult(result); }
        }
        finally { File.Delete(mono); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeTurn { public float Start, End; public int Speaker; public float Confidence; }
    private sealed class DiarizationHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private DiarizationHandle() : base(true) { }
        public static DiarizationHandle Create(OfflineSpeakerDiarizationConfig config)
        {
            var value = Native.Create(ref config); if (value == 0) throw new InvalidOperationException("Could not load the speaker models.");
            var handle = new DiarizationHandle(); handle.SetHandle(value); return handle;
        }
        protected override bool ReleaseHandle() { Native.Destroy(handle); return true; }
    }
    private static class Native
    {
        private const string Library = "sherpa-onnx-c-api";
        [DllImport(Library, EntryPoint = "SherpaOnnxCreateOfflineSpeakerDiarization", CallingConvention = CallingConvention.Cdecl)] public static extern nint Create(ref OfflineSpeakerDiarizationConfig config);
        [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOfflineSpeakerDiarization", CallingConvention = CallingConvention.Cdecl)] public static extern void Destroy(nint handle);
        [DllImport(Library, EntryPoint = "SherpaOnnxOfflineSpeakerDiarizationGetSampleRate", CallingConvention = CallingConvention.Cdecl)] public static extern int SampleRate(DiarizationHandle handle);
        [DllImport(Library, EntryPoint = "SherpaOnnxOfflineSpeakerDiarizationProcess", CallingConvention = CallingConvention.Cdecl)] public static extern nint Process(DiarizationHandle handle, float[] samples, int length);
        [DllImport(Library, EntryPoint = "SherpaOnnxOfflineSpeakerDiarizationResultGetNumSegments", CallingConvention = CallingConvention.Cdecl)] public static extern int Count(nint result);
        [DllImport(Library, EntryPoint = "SherpaOnnxOfflineSpeakerDiarizationResultSortByStartTime", CallingConvention = CallingConvention.Cdecl)] public static extern nint Sort(nint result);
        [DllImport(Library, EntryPoint = "SherpaOnnxOfflineSpeakerDiarizationDestroyResult", CallingConvention = CallingConvention.Cdecl)] public static extern void DestroyResult(nint result);
        [DllImport(Library, EntryPoint = "SherpaOnnxOfflineSpeakerDiarizationDestroySegment", CallingConvention = CallingConvention.Cdecl)] public static extern void DestroySegments(nint segments);
    }
}
