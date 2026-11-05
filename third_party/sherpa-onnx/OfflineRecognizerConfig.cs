// Murmur: UTF-8 marshalling for Windows paths. Upstream source is pinned in PROVENANCE.md.
/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineRecognizerConfig
    {
        public OfflineRecognizerConfig()
        {
            FeatConfig = new FeatureConfig();
            ModelConfig = new OfflineModelConfig();
            LmConfig = new OfflineLMConfig();

            DecodingMethod = "greedy_search";
            MaxActivePaths = 4;
            HotwordsFile = "";
            HotwordsScore = 1.5F;
            RuleFsts = "";
            RuleFars = "";
            BlankPenalty = 0.0F;
            Hr = new HomophoneReplacerConfig();
            CtcFstDecoderConfig = new OfflineCtcFstDecoderConfig();
        }
        public FeatureConfig FeatConfig;
        public OfflineModelConfig ModelConfig;
        public OfflineLMConfig LmConfig;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string DecodingMethod;

        public int MaxActivePaths;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string HotwordsFile;

        public float HotwordsScore;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string RuleFsts;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string RuleFars;

        public float BlankPenalty;

        public HomophoneReplacerConfig Hr;

        public OfflineCtcFstDecoderConfig CtcFstDecoderConfig;
    }
}
