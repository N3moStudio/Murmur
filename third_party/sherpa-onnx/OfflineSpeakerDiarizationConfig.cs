// Adapted: UTF-8 interop for Windows paths. Upstream revision af2d8d81dbfa61c4cea4c568da78f3982edca005.
/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace SherpaOnnx
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeakerDiarizationConfig
    {
        public OfflineSpeakerDiarizationConfig()
        {
            Segmentation = new OfflineSpeakerSegmentationModelConfig();
            Embedding = new SpeakerEmbeddingExtractorConfig();
            Clustering = new FastClusteringConfig();

            MinDurationOn = 0.3F;
            MinDurationOff = 0.5F;
        }

        public OfflineSpeakerSegmentationModelConfig Segmentation;
        public SpeakerEmbeddingExtractorConfig Embedding;
        public FastClusteringConfig Clustering;

        public float MinDurationOn;
        public float MinDurationOff;
    }
}



