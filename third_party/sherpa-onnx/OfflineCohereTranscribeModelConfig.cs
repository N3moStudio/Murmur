// Murmur: UTF-8 marshalling for Windows paths. Upstream source is pinned in PROVENANCE.md.
/// Copyright (c)  2026  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineCohereTranscribeModelConfig
    {
        public OfflineCohereTranscribeModelConfig()
        {
            Encoder = "";
            Decoder = "";
            Language = "";
            UsePunct = 1;
            UseItn = 1;
        }

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Encoder;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Decoder;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Language;

        public int UsePunct;
        public int UseItn;
    }
}
