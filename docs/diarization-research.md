# Local multi-speaker diarization for Murmur

Checked 2026-11-04. **A practical native Windows ARM64 and x64 path exists:** sherpa-onnx 2.0.1 CPU diarization, the documented MIT Pyannote 3.0 ONNX conversion, and the Apache-2.0 ERes2Net-Base speaker embedding model. This can add actual voice clustering after a meeting; mic/system channel labels alone are not diarization.

This investigation downloaded only metadata, licenses and source. No native executable/library or model was downloaded or executed. Runtime compatibility, throughput, RAM and diarization accuracy remain untested on this PC. No app source was changed.

## Pinned engine and packages

Official release: [sherpa-onnx v2.0.1](https://github.com/k2-fsa/sherpa-onnx/releases/tag/v2.0.1), revision **af2d8d81dbfa61c4cea4c568da78f3982edca005**. Engine license: [Apache-2.0](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/LICENSE). Retain engine and native dependency notices; ONNX Runtime has its [own MIT license](https://github.com/microsoft/onnxruntime/blob/main/LICENSE).

The easiest C# route is [`org.k2fsa.sherpa.onnx` 2.0.1](https://www.nuget.org/packages/org.k2fsa.sherpa.onnx/2.0.1). Its actual nuspec provides net10.0/net8.0/netstandard2.0 and other targets, and references both Windows architecture runtime packages. NuGet catalog file lists verify:

| Package | Download size | Actual package entries |
|---|---:|---|
| `org.k2fsa.sherpa.onnx` | 211,228 bytes | `lib/net10.0/sherpa-onnx.dll` and other TFMs |
| `org.k2fsa.sherpa.onnx.runtime.win-arm64` | 8,029,645 bytes | `runtimes/win-arm64/native/onnxruntime.dll`, `sherpa-onnx-c-api.dll` |
| `org.k2fsa.sherpa.onnx.runtime.win-x64` | 8,413,229 bytes | Corresponding `win-x64` DLLs |

Exact NuGet download URLs and authoritative catalog **SHA-512 base64** hashes are saved in [diarization-downloads.json](diarization-downloads.json). These are publisher/catalog hashes, not locally computed package hashes. The managed package references runtimes for other platforms too; verify RID-specific publish output does not ship those unnecessarily.

Exact managed dependency graph: `org.k2fsa.sherpa.onnx` 2.0.1 depends on all nine 2.0.1 runtime packages: `android-arm64`, `android-x64`, `linux-arm64`, `linux-x64`, `osx-arm64`, `osx-x64`, `win-arm64`, `win-x64`, `win-x86`. The two Windows native runtime packages themselves have empty dependency groups. A normal managed PackageReference therefore restores all platform packages, although explicit RID publishing selects native assets for that RID. Adding direct Windows references does **not** remove the other transitive dependencies.

For the selected same-app isolated `--diarize` child, a checked custom C API/SafeHandle binding can omit the upstream managed package entirely and reference only the matching native package. This also avoids its unchecked-null-handle constructor. Suggested project fragment (documentation only):

```xml
<ItemGroup Condition="'$(RuntimeIdentifier)' == 'win-arm64'">
  <PackageReference Include="org.k2fsa.sherpa.onnx.runtime.win-arm64" Version="2.0.1" />
</ItemGroup>
<ItemGroup Condition="'$(RuntimeIdentifier)' == 'win-x64'">
  <PackageReference Include="org.k2fsa.sherpa.onnx.runtime.win-x64" Version="2.0.1" />
</ItemGroup>
```

Build/publish with an explicit RID so one condition is true. These native packages supply two DLLs per RID, not a managed `SherpaOnnx` namespace; the custom binding must declare the verified sequential C structs and entry points. Alternatively retain the upstream managed package and accept its broader restore graph. Do not add `Microsoft.ML.OnnxRuntime` as a second runtime provider: the sherpa package already supplies its compatible `onnxruntime.dll`.

Alternative official no-TTS shared-library archives:

| Architecture | Download | Bytes | Publisher SHA-256 |
|---|---|---:|---|
| ARM64 | [MT Release no-TTS libs](https://github.com/k2-fsa/sherpa-onnx/releases/download/v2.0.1/sherpa-onnx-v2.0.1-win-arm64-shared-MT-Release-no-tts-lib.tar.bz2) | 7,310,187 | `b3099bec60e6c3958275e90d0803d837cfe3ad6ed08528f29505d713b3a3e1bb` |
| x64 | [MT Release no-TTS libs](https://github.com/k2-fsa/sherpa-onnx/releases/download/v2.0.1/sherpa-onnx-v2.0.1-win-x64-shared-MT-Release-no-tts-lib.tar.bz2) | 7,655,517 | `21da6a8b92d63ca02ad41dbfc120012343aaaec2e5e02b14819c6b4f38004509` |

[ARM64 workflow](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/.github/workflows/windows-arm64.yaml#L68) builds with `-A ARM64`; [x64 workflow](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/.github/workflows/windows-x64.yaml#L68) uses `-A x64`. [CMake defaults](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/CMakeLists.txt#L72) enable speaker diarization, and these workflows do not switch it off. This is native ARM64 build evidence, not ARM64EC/emulated-x64 evidence; actual PE headers and inference were not checked. Prefer one consistent NuGet route or archive route and avoid mixing versions.

## Public model candidates and license provenance

**Recommended candidate pair for validation:** `model.int8.onnx` segmentation + `3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx` embedding, total 41,134,267 bytes. Use float32 segmentation if the quantized version regresses quality.

| Role | File / size | License and provenance |
|---|---|---|
| Segmentation | `model.int8.onnx`, 1,540,506 bytes; optional float `model.onnx`, 5,992,913 bytes | [Maintainer's conversion](https://huggingface.co/csukuangfj/sherpa-onnx-pyannote-segmentation-3-0/tree/9403a6902bb58e3d5ae8c7e77c3422de279db2e0); explicit [MIT LICENSE, copyright 2022 CNRS](https://huggingface.co/csukuangfj/sherpa-onnx-pyannote-segmentation-3-0/blob/9403a6902bb58e3d5ae8c7e77c3422de279db2e0/LICENSE) |
| Speaker embedding | `3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx`, 39,593,761 bytes | [Sherpa maintainer's export](https://huggingface.co/csukuangfj/speaker-embedding-models/blob/0743f301363dec56491a490f6d6cbc9d67f9a3bf/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx); [exact original ModelScope model card](https://modelscope.cn/api/v1/models/iic/speech_eres2net_base_sv_zh-cn_3dspeaker_16k/repo?Revision=v1.0.1&FilePath=README.md) declares Apache License 2.0 |
| Alternative embedding | `nemo_en_titanet_large.onnx`, 101,405,493 bytes | [Maintainer's export](https://huggingface.co/csukuangfj/speaker-embedding-models/blob/0743f301363dec56491a490f6d6cbc9d67f9a3bf/nemo_en_titanet_large.onnx); [NVIDIA original model card](https://huggingface.co/nvidia/speakerverification_en_titanet_large/blob/0dc382f40121a5fbd34db10a2bb04d826c2be6a8/README.md) declares CC-BY-4.0; retain attribution/license and identify ONNX conversion |

Exact immutable Hugging Face resolve URLs and LFS SHA-256 hashes are in the download manifest. The segmentation revision is `9403a6902bb58e3d5ae8c7e77c3422de279db2e0`; embeddings revision is `0743f301363dec56491a490f6d6cbc9d67f9a3bf`. Both API responses explicitly report `gated: false`. Hashes are metadata evidence; verify actual files before loading. The conversion does not record the exact original ERes2Net checkpoint revision, so do not claim that the v1.0.1 card proves its build origin. It does establish licensing for the identified upstream model.

The original [Pyannote segmentation-3.0](https://huggingface.co/pyannote/segmentation-3.0/tree/main) Hugging Face repository is gated for contact-sharing even though its license is MIT. The candidate above is the separately published public ONNX distribution documented by the [official sherpa diarization guide](https://k2-fsa.github.io/sherpa/onnx/speaker-diarization/index.html), retaining the MIT license. This investigation did not access gated files, use a token, accept conditions for the user, or bypass that gate. Do not use the original gated download endpoint for unattended provisioning.

**Exclude Reverb as the default.** Its original model is gated and its [Rev Model Non-Production License](https://huggingface.co/Revai/reverb-diarization-v1/blob/main/LICENSE) restricts use, derivatives and outputs; the freely downloadable sherpa conversion does not remove those conditions. It is not equivalent to an unrestricted open-source production model.

## Exact C# API and minimal configuration

Official [C# example](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/dotnet-examples/offline-speaker-diarization/Program.cs) uses the same Pyannote + ERes2Net pair. Source bindings are under [`scripts/dotnet`](https://github.com/k2-fsa/sherpa-onnx/tree/af2d8d81dbfa61c4cea4c568da78f3982edca005/scripts/dotnet), namespace `SherpaOnnx`, P/Invoke library `sherpa-onnx-c-api`.

Configuration sketch (documentation only; not executed):

```csharp
var config = new OfflineSpeakerDiarizationConfig();
config.Segmentation.Pyannote.Model = segmentationPath;
config.Segmentation.Pyannote.WindowShiftRatio = 0.1f;
config.Segmentation.Provider = "cpu";
config.Segmentation.NumThreads = 2;
config.Embedding.Model = embeddingPath;
config.Embedding.Provider = "cpu";
config.Embedding.NumThreads = 2;
config.Clustering.NumClusters = -1; // unknown speaker count
config.Clustering.Threshold = 0.5f; // tune on representative audio
config.Clustering.ComputeConfidence = 1;
config.MinDurationOn = 0.3f;
config.MinDurationOff = 0.5f;
```

[`OfflineSpeakerDiarization`](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/scripts/dotnet/OfflineSpeakerDiarization.cs) exposes constructor `(OfflineSpeakerDiarizationConfig)`, `int SampleRate`, `Process(float[])`, `ProcessWithCallback(float[], OfflineSpeakerDiarizationProgressCallback, IntPtr)`, `SetConfig(config)`, `Dispose()`. Both processing methods return `OfflineSpeakerDiarizationSegment[]`, sorted by start time. The delegate signature is `int callback(int numProcessedChunks, int numTotalChunks, IntPtr arg)`.

Each segment has `float Start`, `float End` (seconds), `int Speaker`, and `float Confidence`. Confidence is a clustering silhouette-derived score in [-1,1], **not a probability**; -2 means unavailable. A positive `NumClusters` supplies a known speaker count; otherwise tune `Threshold`. Input is mono float PCM normalized to [-1,1] at the model's sample rate (these candidates use 16 kHz). Configuration defaults use one thread; two is a starting validation choice, not a measured optimum.

## Required integration care and limits

- **Check native construction.** The 2.0.1 managed constructor stores the native pointer without checking it. [C API creation](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/sherpa-onnx/c-api/c-api.cc#L3419) returns null on model/config failure; subsequent SampleRate/Process dereference it. A checked SafeHandle wrapper or isolated worker is preferable to assuming a managed exception. Verify downloads, config and exports before invoking.
- **Cancellation is cooperative only outside this API.** The [C header](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/sherpa-onnx/c-api/c-api.h#L4168) explicitly says callback return values are ignored. Run off the UI thread, keep callback/model alive until completion, discard obsolete results, and never dispose while a call is active. True hard cancellation requires a worker process.
- **This is offline clustering.** Loading one entire meeting permits globally consistent speaker clusters. Independent chunks receive unrelated IDs; splitting for memory requires additional embedding/cluster reconciliation. A one-hour mono16k float array alone costs 230,400,000 bytes (~220 MiB), before inference/clustering buffers. Load only for labeling and dispose afterward; avoid competing simultaneously with Whisper and the cleanup LLM on this 16 GB machine.
- **It returns speaker turns, not text or identities.** Align Whisper timestamps to diarization intervals, retain overlapping speakers/uncertainty, and let the user rename generic Speaker 1/2 labels. Cluster IDs are local to a processing run. Do not assign a real person's name without user-provided mapping.
- **Channel labels remain useful provenance.** Keep mic/system tracks synchronized and distinguish their source from inferred speaker identity. Mixing can produce duplicate voices through acoustic echo; separate processing requires cross-track cluster reconciliation. Merely naming mic/system tracks does not fulfill multiple speakers on one track.
- **Quality requires validation.** Overlap, very short interjections, similar voices, reverberation, language/domain differences and background media can split or merge speakers. The ERes2Net candidate's training/model card is Chinese/3D-Speaker; benchmark English meetings too. TitaNet is an alternative with an explicit English model card, not a proven improvement here. A faster window stride can reduce compute but may change accuracy.

Minimum meaningful validation: public/licensed two- and four-speaker clips with reference turns, one-speaker and silence controls, mixed mic/system alignment, overlap, short replies, long-meeting RAM, canceled run followed by new work, corrupt/missing model failure, and actual ARM64/x64 native loading. Compare timestamp assignment and cluster count to reference labels, not just successful inference. No such checks were performed in this research.

## Saved evidence

`research/diarization-reference/` contains pinned C# and C++ sources, build workflows, license/model cards, complete GitHub release metadata, NuGet catalog entries and HF LFS metadata. `source-manifest.json` records source hashes. [diarization-downloads.json](diarization-downloads.json) is the machine-readable package/model candidate manifest for implementation; it is not an app configuration or evidence that models have been installed.

Full primary license texts are saved in `research/diarization-reference/licenses/`: sherpa Apache-2.0, Pyannote MIT/CNRS copyright, ONNX Runtime 1.28.2 MIT and its complete ThirdPartyNotices, Apache-2.0 text for ERes2Net, and the exact ERes2Net v1.0.1 model card. The engine's architecture-specific CMake files both pin ONNX Runtime 1.28.2. Preserve copyright/attribution and any additional notices found in the final native distribution; the six saved documents are not a claim that every sherpa statically linked dependency has been inventoried. The optional TitaNet candidate additionally needs CC-BY-4.0 attribution/license if selected.
