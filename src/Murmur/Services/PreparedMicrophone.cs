using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Murmur.Services;

// Initialize the device during the first half of the shortcut. No audio is
// captured until StartRecording, after the completed double-tap. An abandoned
// gesture releases this client promptly; nothing stays open between gestures.
internal sealed class PreparedMicrophone : IWaveIn
{
    private readonly MMDevice device;
    private readonly AudioClient client;
    private readonly AutoResetEvent packetReady = new(false);
    private readonly byte[] buffer;
    private Task? pump;
    private int running, disposed;
    public WaveFormat WaveFormat { get; set; }
    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public PreparedMicrophone(string microphone)
    {
        using var enumerator = new MMDeviceEnumerator();
        device = microphone == "default" ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(microphone);
        try
        {
            client = device.AudioClient;
            try
            {
                var format = client.MixFormat;
                client.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.EventCallback, 20 * 10000, 0, format, Guid.Empty);
                client.SetEventHandle(packetReady.SafeWaitHandle.DangerousGetHandle());
                WaveFormat = format.AsStandardWaveFormat();
                buffer = new byte[client.BufferSize * WaveFormat.BlockAlign];
            }
            catch { client.Dispose(); throw; }
        }
        catch { device.Dispose(); packetReady.Dispose(); throw; }
    }
    public void StartRecording()
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (Interlocked.Exchange(ref running, 1) != 0) throw new InvalidOperationException("Recording is already active.");
        try { client.Start(); pump = Task.Run(ReadPackets); }
        catch { running = 0; throw; }
    }
    private void ReadPackets()
    {
        Exception? error = null;
        try
        {
            var capture = client.AudioCaptureClient;
            while (true)
            {
                packetReady.WaitOne(60);
                while (capture.GetNextPacketSize() > 0)
                {
                    nint samples = capture.GetBuffer(out int frames, out AudioClientBufferFlags flags);
                    int count = frames * WaveFormat.BlockAlign;
                    try
                    {
                        if (count > buffer.Length) throw new InvalidDataException("The microphone returned an oversized audio packet.");
                        if ((flags & AudioClientBufferFlags.Silent) != 0) Array.Clear(buffer, 0, count);
                        else Marshal.Copy(samples, buffer, 0, count);
                    }
                    finally { capture.ReleaseBuffer(frames); }
                    if (count > 0) DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, count));
                }
                if (Volatile.Read(ref running) == 0) break;
            }
        }
        catch (Exception e) { error = e; }
        finally
        {
            Interlocked.Exchange(ref running, 0);
            try { client.Stop(); } catch (Exception e) { error ??= e; }
            RecordingStopped?.Invoke(this, new StoppedEventArgs(error));
        }
    }
    public void StopRecording() { Interlocked.Exchange(ref running, 0); packetReady.Set(); }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        StopRecording(); pump?.GetAwaiter().GetResult();
        client.Dispose(); device.Dispose(); packetReady.Dispose();
    }
}

internal sealed class MicrophonePrimer : IDisposable
{
    private Task<PreparedMicrophone>? preparation;
    private string? deviceId;
    private bool disposed;
    public void Prepare(string microphone)
    {
        if (disposed || preparation is not null) return;
        deviceId = microphone;
        var task = preparation = Task.Run(() => new PreparedMicrophone(microphone));
        _ = Expire(task);
    }
    private async Task Expire(Task<PreparedMicrophone> task)
    {
        await Task.Delay(900);
        if (preparation != task) return;
        preparation = null;
        await Release(task);
    }
    private static async Task Release(Task<PreparedMicrophone> task)
    {
        try { (await task).Dispose(); } catch (Exception) { /* No confirmed recording to report an error for. */ }
    }
    public async Task<PreparedMicrophone> Take(string microphone)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var task = preparation; preparation = null;
        if (task is not null && deviceId == microphone) return await task;
        if (task is not null) _ = Release(task);
        return await Task.Run(() => new PreparedMicrophone(microphone));
    }
    public void Dispose() { disposed = true; var task = preparation; preparation = null; if (task is not null) _ = Release(task); }
}
