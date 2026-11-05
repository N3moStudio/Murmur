using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SherpaOnnx;

namespace Murmur.Services;

// Calls are serialized by SpeechService. Handles cannot be disposed mid-decode.
internal sealed class FastSpeechEngine : IDisposable
{
    private readonly RecognizerHandle recognizer;
    public FastSpeechEngine(ModelManager models)
    {
        var config = new OfflineRecognizerConfig();
        config.ModelConfig.Moonshine.Encoder = models.PathFor(ModelManager.FastEnglishFiles[0]);
        config.ModelConfig.Moonshine.MergedDecoder = models.PathFor(ModelManager.FastEnglishFiles[1]);
        config.ModelConfig.Tokens = models.PathFor(ModelManager.FastEnglishFiles[2]);
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.NumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        config.DecodingMethod = "greedy_search";
        recognizer = RecognizerHandle.Create(config);
    }
    public string Decode(float[] samples, int length, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var stream = StreamHandle.Create(recognizer);
        Native.Accept(stream, 16000, samples, length);
        Native.Decode(recognizer, stream);
        ct.ThrowIfCancellationRequested();
        nint result = Native.GetResult(stream);
        if (result == 0) throw new InvalidDataException("The English speech engine returned no result.");
        try
        {
            // First member of the pinned C ABI is a NUL-terminated UTF-8 text
            // pointer. We need no token/timestamp arrays for this model family.
            nint value = Marshal.ReadIntPtr(result);
            return value == 0 ? "" : (Marshal.PtrToStringUTF8(value) ?? "").Trim();
        }
        finally { Native.DestroyResult(result); }
    }
    public void Dispose() => recognizer.Dispose();
    private sealed class RecognizerHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private RecognizerHandle() : base(true) { }
        public static RecognizerHandle Create(OfflineRecognizerConfig config)
        {
            nint pointer = Native.Create(ref config);
            if (pointer == 0) throw new InvalidOperationException("The English speech model could not load. Check its download and repair the app if needed.");
            var result = new RecognizerHandle(); result.SetHandle(pointer); return result;
        }
        protected override bool ReleaseHandle() { Native.Destroy(handle); return true; }
    }
    private sealed class StreamHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private StreamHandle() : base(true) { }
        public static StreamHandle Create(RecognizerHandle recognizer)
        {
            nint pointer = Native.CreateStream(recognizer);
            if (pointer == 0) throw new InvalidOperationException("The speech decoder could not start.");
            var result = new StreamHandle(); result.SetHandle(pointer); return result;
        }
        protected override bool ReleaseHandle() { Native.DestroyStream(handle); return true; }
    }
    private static class Native
    {
        private const string Library = "sherpa-onnx-c-api";
        [DllImport(Library, EntryPoint = "SherpaOnnxCreateOfflineRecognizer", CallingConvention = CallingConvention.Cdecl)] public static extern nint Create(ref OfflineRecognizerConfig config);
        [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOfflineRecognizer", CallingConvention = CallingConvention.Cdecl)] public static extern void Destroy(nint handle);
        [DllImport(Library, EntryPoint = "SherpaOnnxCreateOfflineStream", CallingConvention = CallingConvention.Cdecl)] public static extern nint CreateStream(RecognizerHandle handle);
        [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOfflineStream", CallingConvention = CallingConvention.Cdecl)] public static extern void DestroyStream(nint handle);
        [DllImport(Library, EntryPoint = "SherpaOnnxAcceptWaveformOffline", CallingConvention = CallingConvention.Cdecl)] public static extern void Accept(StreamHandle stream, int sampleRate, float[] samples, int length);
        [DllImport(Library, EntryPoint = "SherpaOnnxDecodeOfflineStream", CallingConvention = CallingConvention.Cdecl)] public static extern void Decode(RecognizerHandle handle, StreamHandle stream);
        [DllImport(Library, EntryPoint = "SherpaOnnxGetOfflineStreamResult", CallingConvention = CallingConvention.Cdecl)] public static extern nint GetResult(StreamHandle stream);
        [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOfflineRecognizerResult", CallingConvention = CallingConvention.Cdecl)] public static extern void DestroyResult(nint result);
    }
}
