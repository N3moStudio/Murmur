// Murmur: UTF-8 marshalling for Windows paths. Upstream source is pinned in PROVENANCE.md.
/// Copyright (c)  2026  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineFireRedAsrCtcModelConfig
    {
        public OfflineFireRedAsrCtcModelConfig()
        {
            Model = "";
        }
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Model;
    }
}
