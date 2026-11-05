// Adapted: UTF-8 interop for Windows paths. Upstream revision af2d8d81dbfa61c4cea4c568da78f3982edca005.
/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace SherpaOnnx
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeakerSegmentationPyannoteModelConfig
    {
        public OfflineSpeakerSegmentationPyannoteModelConfig()
        {
            Model = "";
            WindowShiftRatio = 0.1f;
        }

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Model;
        public float WindowShiftRatio;
    }
}
