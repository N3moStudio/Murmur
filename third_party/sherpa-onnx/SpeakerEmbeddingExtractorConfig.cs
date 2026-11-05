// Adapted: UTF-8 interop for Windows paths. Upstream revision af2d8d81dbfa61c4cea4c568da78f3982edca005.
/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SpeakerEmbeddingExtractorConfig
    {
        public SpeakerEmbeddingExtractorConfig()
        {
            Model = "";
            NumThreads = 1;
            Debug = 0;
            Provider = "cpu";
        }

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Model;

        public int NumThreads;
        public int Debug;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Provider;
    }

}