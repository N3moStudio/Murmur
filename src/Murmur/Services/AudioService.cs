using Murmur.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Murmur.Services;

public sealed class AudioService : IDisposable
{
    private readonly List<Track> tracks = [];
    private readonly object gate = new();
    public event Action<float>? Level;
    public event Action<string>? Fault;
    public event Action? FirstPacket;
    public bool IsRecording { get; private set; }
    public DateTimeOffset Started { get; private set; }
    private long lastSoundTicks;
    private long microphoneBytes;
    public long FirstPacketTimestamp { get; private set; }
    public long OpenStartedTimestamp { get; private set; }
    public long DeviceSelectedTimestamp { get; private set; }
    public long ClientCreatedTimestamp { get; private set; }
    public long StartReturnedTimestamp { get; private set; }
    public long MicrophoneBytes => Interlocked.Read(ref microphoneBytes);
    public long SoundRevision => Interlocked.Read(ref lastSoundTicks);
    public double QuietMilliseconds => TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref lastSoundTicks)).TotalMilliseconds;
    public bool SnapshotMicrophone(string destination, CancellationToken ct)
    {
        FileStream input; WaveFormat format; long remaining;
        lock (gate)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsRecording || tracks.Count == 0) return false;
            var track = tracks[0]; track.Writer.Flush();
            remaining = track.Writer.Length; format = track.Capture.WaveFormat;
            input = new FileStream(track.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            input.Position = input.Length - remaining;
        }
        // WAV sample data is append-only. Copy a bounded prefix outside the
        // capture lock, with a fresh header, so slow disk cannot block capture.
        using (input)
        using (var output = new WaveFileWriter(destination, format))
        {
            var buffer = new byte[65536];
            while (remaining > 0) { ct.ThrowIfCancellationRequested(); int n = input.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length)); if (n == 0) throw new EndOfStreamException("Incomplete recording snapshot."); output.Write(buffer, 0, n); remaining -= n; }
        }
        return true;
    }
    private sealed class Track(IWaveIn capture, WaveFileWriter writer, string path, string speaker, MMDevice? device)
    {
        public IWaveIn Capture = capture; public WaveFileWriter Writer = writer; public string Path = path, Speaker = speaker;
        public TaskCompletionSource Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public MMDevice? Device = device;
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) != 0) return; try { Capture.StopRecording(); } catch { } Capture.Dispose(); Writer.Dispose(); Device?.Dispose(); Done.TrySetResult(); }
    }
    public static object Devices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Select(d => { var result = new { id = d.ID, name = d.FriendlyName }; d.Dispose(); return result; }).ToArray();
    }
    public void Start(string folder, string microphone, bool systemAudio, IWaveIn? preparedMicrophone = null)
    {
        if (IsRecording) throw new InvalidOperationException("Recording is already active.");
        OpenStartedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        Directory.CreateDirectory(folder); Started = DateTimeOffset.UtcNow; Interlocked.Exchange(ref lastSoundTicks, DateTime.UtcNow.Ticks);
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            DeviceSelectedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (preparedMicrophone is not null) Add(preparedMicrophone, Path.Combine(folder, "microphone.wav"), "You");
            else
            {
                var device = microphone == "default" ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(microphone);
                try { Add(new WasapiCapture(device, true, 20), Path.Combine(folder, "microphone.wav"), "You", device); } catch { device.Dispose(); throw; }
            }
            ClientCreatedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (systemAudio) Add(new WasapiLoopbackCapture(), Path.Combine(folder, "system.wav"), "System audio");
            IsRecording = true;
            foreach (var track in tracks) track.Capture.StartRecording();
            StartReturnedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        catch { Dispose(); throw; }
    }
    private void Add(IWaveIn capture, string path, string speaker, MMDevice? device = null)
    {
        WaveFileWriter writer; try { writer = new(path, capture.WaveFormat); } catch { capture.Dispose(); throw; }
        var track = new Track(capture, writer, path, speaker, device); tracks.Add(track);
        var lastLevel = DateTime.UtcNow;
        capture.DataAvailable += (_, e) =>
        {
            try {
            lock (gate)
            {
                if (!IsRecording) return;
                // Loopback can be silent without producing packets; pad gaps to the common clock.
                if (speaker == "System audio")
                {
                    var expected = (long)((DateTimeOffset.UtcNow - Started).TotalSeconds * capture.WaveFormat.AverageBytesPerSecond) - e.BytesRecorded;
                    long missing = expected - track.Writer.Length;
                    if (missing > capture.WaveFormat.AverageBytesPerSecond / 4)
                    {
                        var silence = new byte[8192]; missing -= missing % capture.WaveFormat.BlockAlign;
                        while (missing > 0) { int n = (int)Math.Min(missing, silence.Length); n -= n % capture.WaveFormat.BlockAlign; if (n == 0) break; track.Writer.Write(silence, 0, n); missing -= n; }
                    }
                }
                track.Writer.Write(e.Buffer, 0, e.BytesRecorded);
                if (speaker == "You" && e.BytesRecorded > 0)
                {
                    long total = Interlocked.Add(ref microphoneBytes, e.BytesRecorded);
                    if (total == e.BytesRecorded) { FirstPacketTimestamp = System.Diagnostics.Stopwatch.GetTimestamp(); FirstPacket?.Invoke(); }
                }
                if ((DateTime.UtcNow - lastLevel).TotalMilliseconds >= 65)
                {
                    float peak = 0; var fmt = capture.WaveFormat;
                    if (fmt.BitsPerSample == 32) for (int i = 0; i + 4 <= e.BytesRecorded; i += 4) peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(e.Buffer, i)));
                    else if (fmt.BitsPerSample == 16) for (int i = 0; i + 2 <= e.BytesRecorded; i += 2) peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f));
                    if (speaker == "You" && peak > .025f) Interlocked.Exchange(ref lastSoundTicks, DateTime.UtcNow.Ticks);
                    Level?.Invoke(Math.Clamp(peak, 0, 1)); lastLevel = DateTime.UtcNow; track.Writer.Flush();
                }
            }
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Fault?.Invoke("Recording could not be saved. Check free disk space. Captured audio is available for recovery."); }
        };
        capture.RecordingStopped += (_, e) => { track.Done.TrySetResult(); if (e.Exception is not null) Fault?.Invoke("The audio device stopped. Your captured audio is saved for recovery."); };
    }
    public async Task<List<(string Path, string Speaker)>> Stop()
    {
        Track[] captured; lock (gate) { if (!IsRecording) return []; captured = tracks.ToArray(); }
        // Allow the final event-driven audio packet to arrive before stopping;
        // WASAPI's stop path otherwise discards a pending end consonant.
        await Task.Delay(20);
        foreach (var track in captured) track.Capture.StopRecording();
        await Task.WhenAll(captured.Select(t => t.Done.Task)).WaitAsync(TimeSpan.FromSeconds(5));
        lock (gate) { IsRecording = false; tracks.Clear(); }
        foreach (var track in captured) track.Dispose();
        return captured.Select(t => (t.Path, t.Speaker)).ToList();
    }
    public static string ToMono16(string input, string output, CancellationToken ct = default)
    {
        using var reader = new AudioFileReader(input);
        ISampleProvider mono = reader;
        if (reader.WaveFormat.Channels == 2) mono = new StereoToMonoSampleProvider(reader);
        else if (reader.WaveFormat.Channels != 1) throw new NotSupportedException("Use a mono or stereo audio file.");
        var resampled = new WdlResamplingSampleProvider(mono, 16000);
        using var writer = new WaveFileWriter(output, new WaveFormat(16000, 16, 1));
        var pcm = new SampleToWaveProvider16(resampled); var buffer = new byte[32000]; int n;
        while ((n = pcm.Read(buffer, 0, buffer.Length)) > 0) { ct.ThrowIfCancellationRequested(); writer.Write(buffer, 0, n); }
        return output;
    }
    public static bool HasSignal(string path)
    {
        using var reader = new AudioFileReader(path); var samples = new float[16000]; int n; double energy = 0; long count = 0; float peak = 0;
        while ((n = reader.Read(samples, 0, samples.Length)) > 0) { for (int i = 0; i < n; i++) { energy += samples[i] * samples[i]; peak = Math.Max(peak, Math.Abs(samples[i])); } count += n; }
        return count > 1600 && peak > .012 && Math.Sqrt(energy / count) > .0015;
    }
    public void Dispose()
    {
        Track[] captured; lock (gate) { IsRecording = false; captured = tracks.ToArray(); tracks.Clear(); }
        // WASAPI disposal can wait for a callback. Never hold its write lock here.
        foreach (var track in captured) track.Dispose();
    }
}
