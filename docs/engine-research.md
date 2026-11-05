# Offline engine research

**Release decision:** this early candidate study is retained as research evidence. The shipped English default is Moonshine base-en via sherpa-onnx 2.0.1; see fast-asr-research.md and testing.md for measured results. Whisper remains an explicit alternative. The writing engine remains native llama.cpp b12110.

Research date: 2026-11-04 (America/Los_Angeles). This document records public upstream evidence, not results of running inference on this PC. No models or native engines were downloaded or executed by this research subtask. The lead must validate the selected critical path before accepting the stack.

## Recommended candidate, pending execution

Use a native Windows .NET application, managed WASAPI capture, whisper.cpp for ASR, and a separately isolated llama.cpp process for cleanup, commands and meeting summaries. Pin each engine, model revision and checksum. Keep ASR and text generation in separate directories: their identically named ggml DLLs are not necessarily ABI compatible. Native child processes with hidden windows also isolate model failures from the UI.

This is an engineering recommendation, not a claim that local models match proprietary Wispr output. A small LLM can rewrite numbers or hallucinate corrections. Preserve the raw transcript, reject unsafe edits to protected tokens, and expose cleanup failures with recovery. Benchmark ASR and cleanup independently.

## Official Windows CPU assets

The latest semantic releases observed were [whisper.cpp v1.9.5](https://github.com/ggml-org/whisper.cpp/releases/tag/v1.9.5), dated October 6, and [llama.cpp v0.9.0](https://github.com/ggml-org/llama.cpp/releases/tag/v0.9.0), dated November 4. Semantic releases point to nightly binaries: whisper b5454 in the release body; llama b12110 through its `nightly-tag.txt`. Do not assume the semantic tag itself has Windows binaries.

| Engine | Native target declared by upstream | ZIP bytes displayed | Asset URL | SHA-256 published with asset |
|---|---|---:|---|---|
| whisper b5454 | ARM64 CPU | 4.17 MB | https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-win-cpu-arm64.zip | `28c37e7b598c3d9bbfef94f3bd67f2ee6f12b7c86f3da5edcf5e4308d615ff6d` |
| whisper b5454 | x64 CPU | 8.52 MB | https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip | `6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb` |
| llama b12110 | ARM64 CPU | 12.6 MB | https://github.com/ggml-org/llama.cpp/releases/download/b12110/llama-b12110-bin-win-cpu-arm64.zip | `f8d20fb07d0b499988ced8963afcd5ded2d2cdd9983b757fbc7e51c4abd722e8` |
| llama b12110 | x64 CPU | 23.6 MB | https://github.com/ggml-org/llama.cpp/releases/download/b12110/llama-b12110-bin-win-cpu-x64.zip | `98901e327c252875accaf2d1bf364e10e868843b0b63468baad1aa945cdc5da2` |

Sources: [whisper asset inventory](https://github.com/ggml-org/whisper.cpp/releases/expanded_assets/b5454), [llama asset inventory](https://github.com/ggml-org/llama.cpp/releases/expanded_assets/b12110). Hashes were parsed from the individual asset rows, not inferred from filenames. GitHub unauthenticated REST API was rate limited during research; the official HTML inventories remained accessible.

**Redistribution issue to resolve:** whisper b5454's [ARM64 build workflow](https://github.com/ggml-org/whisper.cpp/blob/b5454/.github/workflows/release.yml) copies `libomp140.aarch64.dll` from Visual Studio `debug_nonredist`. Microsoft [documents that these LLVM OpenMP DLLs are not redistributable](https://learn.microsoft.com/en-us/cpp/build/reference/openmp-enable-openmp-2-0-support?view=msvc-170). Use this ZIP for validation only until the DLL license/dependency is resolved. For a distributable build, build whisper with `GGML_OPENMP=OFF`, or compile and link an independently redistributable upstream LLVM runtime. Do not copy a Microsoft non-redistributable runtime into the installer. Inspect the final import table and licenses.

## Build routes

Both projects have CPU-only native Windows ARM64 support. These are ARM64 targets, not ARM64EC or x64 emulation claims. Compilation and PE inspection still do not prove execution.

The [llama b12110 build guide](https://github.com/ggml-org/llama.cpp/blob/b12110/docs/build.md) specifies the `arm64-windows-llvm-release` preset and a native ARM64 VS developer shell. Its `GGML_OPENMP_FETCH=ON` option downloads the matching official LLVM runtime and license. The x64 preset is `x64-windows-llvm-release`. MSVC CPU builds may have weaker ARM NEON support; prefer Clang for ARM64. Example proposed commands:

```powershell
# In the appropriate VS tools environment, after checking out the pinned source:
cmake --preset arm64-windows-llvm-release -DGGML_OPENMP_FETCH=ON
cmake --build build-arm64-windows-llvm-release
# x64 in its corresponding toolchain environment:
cmake --preset x64-windows-llvm-release -DGGML_NATIVE=OFF
cmake --build build-x64-windows-llvm-release
```

For whisper, the pinned workflow uses Ninja Multi-Config with `cmake/arm64-windows-llvm.cmake`. Proposed distributable CPU configuration (must build and test):

```powershell
cmake -S . -B build-arm64 -G "Ninja Multi-Config" -DCMAKE_TOOLCHAIN_FILE=cmake/arm64-windows-llvm.cmake -DGGML_NATIVE=OFF -DGGML_OPENMP=OFF -DBUILD_SHARED_LIBS=ON -DWHISPER_SDL2=OFF
cmake --build build-arm64 --config Release
# Use an x64 toolchain/environment for this separate build:
cmake -S . -B build-x64 -A x64 -DGGML_NATIVE=OFF -DGGML_OPENMP=OFF -DWHISPER_SDL2=OFF -DBUILD_SHARED_LIBS=ON -DGGML_BACKEND_DL=ON -DGGML_CPU_ALL_VARIANTS=ON
cmake --build build-x64 --config Release
```

Do not make CUDA, NPU, OpenCL, or Vulkan necessary. Their existence upstream does not establish driver availability or correctness on this PC. Add acceleration only after an explicit device-specific benchmark and fallback test.

## Models and integrity

`researched-models.json` contains pinned Hugging Face repository revisions, exact byte sizes, download URLs and SHA-256 hashes from the upstream LFS objects. These are stronger integrity inputs than the 40-character SHA values in the older whisper model table. Verify actual downloaded bytes before activation, use `.part` staging, and atomically rename only after verification. Allow cancellation/retry; interrupted downloads must not replace an installed model.

| Model | Exact download bytes | Intended selectable tradeoff |
|---|---:|---|
| Whisper tiny multilingual | 77,691,713 | Smallest quick-start/testing option; lower accuracy must be disclosed |
| Whisper base multilingual | 147,951,465 | Initial speed/quality candidate, pending measurements |
| Whisper small multilingual | 487,601,967 | Larger accuracy candidate, slower CPU execution expected |
| Whisper large-v3-turbo Q5_0 | 574,041,195 | Higher quality candidate; actual host latency unknown |
| Qwen2.5-1.5B-Instruct Q4_K_M | 1,117,320,736 | Small cleanup/summarization candidate; not guaranteed faithful |
| Silero VAD v5.1.2 or v6.2.0 | 885,098 each | Speech detection before ASR, especially silence/noise protection |

Whisper models without `.en` are multilingual. Upstream reports approximate runtime memory of 273 MB tiny, 388 MB base, 852 MB small, 2.1 GB medium and 3.9 GB large; these are **upstream estimates, not this app's measurements**. The CLI expects 16-bit WAV; normalize capture to mono 16 kHz PCM. Source: [whisper README](https://github.com/ggml-org/whisper.cpp/blob/b5454/README.md), [model documentation](https://github.com/ggml-org/whisper.cpp/blob/b5454/models/README.md).

The [Whisper conversion repository](https://huggingface.co/ggerganov/whisper.cpp) and [Silero conversions](https://huggingface.co/ggml-org/whisper-vad) declare MIT. [Qwen's official GGUF repository](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF) declares Apache-2.0 and supports over 29 languages. Keep its full license and relevant notices. The Qwen candidate is intentionally modest and older; benchmark a larger/newer model before offering stronger quality claims. Model size is not runtime memory; allow space for staging and model working memory, and measure peak resident usage.

## Alternatives and meeting speakers

[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) is Apache-2.0, uses ONNX Runtime, and supports offline and streaming ASR, VAD, punctuation, denoising and speaker diarization. Official Windows docs list real [ARM64](https://k2-fsa.github.io/sherpa/onnx/install/windows/generated/download/windows_arm64.html) and [x64](https://k2-fsa.github.io/sherpa/onnx/install/windows/generated/download/windows_x64.html) v2.0.1 CPU binaries, with shared/static and MT/MD choices. An ARM64 candidate is `sherpa-onnx-v2.0.1-win-arm64-shared-MT-Release-no-tts.tar.bz2` under the official v2.0.1 GitHub release. It is viable, but every bundled ONNX library and chosen model license still needs inspection.

For real meeting speakers, the [C# diarization example](https://github.com/k2-fsa/sherpa-onnx/blob/master/dotnet-examples/offline-speaker-diarization/Program.cs) combines segmentation and embedding models and returns timed speaker clusters. [The diarization guide](https://k2-fsa.github.io/sherpa/onnx/speaker-diarization/index.html) lists Pyannote/Reverb segmentation with 3D-Speaker or NeMo embedding. Check each model's license before download/redistribution. Labels like “Speaker 1” can represent inferred clusters; do not invent people's names. Whisper tinydiarize detects turns, which is not equivalent to identifying stable speakers.

## Windows UI/audio viability

Microsoft [supports native ARM64 and x64 .NET on Windows 11](https://learn.microsoft.com/en-us/dotnet/core/install/windows). On ARM PCs, normal `Program Files/dotnet` is the ARM64 location; x64 may live in `Program Files/dotnet/x64`. Publish each RID separately with a self-contained desktop runtime and verify shipped native DLLs.

[NAudio](https://github.com/naudio/NAudio) is MIT and provides WASAPI microphone/loopback recording, device enumeration, levels, mixing and resampling. The managed code route avoids an unnecessary x64-only audio addon, but this research has not tested its interop on ARM64. Use actual capture, disconnect, sleep/resume and channel-format tests. Avoid ASIO unless explicitly supporting its separate driver/runtime constraints.

## Acceptance gates before committing

1. Hash each engine archive and model; inventory every EXE/DLL PE machine (`0xAA64` ARM64, `0x8664` AMD64). Reject x64 dependencies from the ARM64 critical path.
2. Run native ARM64 `whisper-cli --help`, then transcribe a labeled real-speech WAV with VAD. Run x64 on this PC only as an **emulation test**. Repeat on physical/VM x64 hardware when available.
3. Record a real microphone sample, not only a fixture. Exercise silence, fan noise, Unicode, a second language, numbers, URL/email/code spelling and repeated cancellation.
4. Measure cold/warm ASR time, cleanup time and total stop-to-insert separately, with model, threads, sample duration, CPU and peak memory recorded. Compare output to a supplied reference transcript using WER plus protected-token checks.
5. For cleanup, validate meaning, numbers, names and corrections. If the model is absent, show a clear missing-model state; do not silently substitute cloud AI or claim a regex-only path is equivalent.
6. Re-run the same audio and cleanup samples without a network connection. Check that no engine attempts model downloads after setup. Test model corruption, cancellation and process failure recovery.
7. Retest packaged binaries after installation without relying on developer PATH or installed SDK libraries. Only this establishes a usable installed inference path.

No accuracy, speed, memory, or end-to-end audio result is claimed here. Remaining blockers are implementation/testing work, not a requirement for paid services.

## Follow-up: validated Whisper.net package contents (preferred integration candidate)

At the lead's request, downloaded and inspected the actual [Whisper.net.Runtime 1.9.1 NuGet package](https://www.nuget.org/packages/Whisper.net.Runtime/1.9.1), managed [Whisper.net 1.9.1](https://www.nuget.org/packages/Whisper.net/1.9.1), and [NoAvx 1.9.1](https://www.nuget.org/packages/Whisper.net.Runtime.NoAvx/1.9.1). Files are retained under `research/dependencies`. This supplements the initial no-download research boundary; **no inference was executed by the researcher**.

Observed ZIP contents and REA PE inspection establish four ARM64 `0xAA64` native DLLs under `build/win-arm64`, and corresponding AMD64 `0x8664` DLLs under `build/win-x64`: `whisper.dll`, `ggml-whisper.dll`, `ggml-base-whisper.dll`, `ggml-cpu-whisper.dll`. All eight import tables were inspected completely. No ARM64 DLL imports `libomp`; their external imports are the standard VC runtime and Windows DLLs. x64 CPU/base DLLs import normal redistributable `VCOMP140.DLL`. This avoids the b5454 ARM64 non-redistributable LLVM OpenMP dependency and is the preferred candidate for the lead's .NET host, pending real execution.

The complete REA evidence records are in `research/dependencies/whisper-net-rea-evidence.json`; compact hashes/imports/limitations and all evidence IDs are in `whisper-net-imports.json`. Example ARM64 CPU evidence ID: `ev_0f998d0326e28b3589b3735fed70e6cc0817cf916ffd0adf07d6f861c5551526`. Static import coverage does not establish runtime load or inference success.

| Package | Download bytes | Observed SHA-256 |
|---|---:|---|
| Whisper.net.Runtime 1.9.1 | 18,482,556 | `b5224f0dad44d5eb8233e5d83f4333a8a3fccadc77095f50f361f06d65e0736b` |
| Whisper.net.Runtime.NoAvx 1.9.1 | 2,531,679 | `42b68b3970522e00eb7e9f9a006ad0055a4bdf741eb318698587e18967c90582` |
| Whisper.net 1.9.1 | 843,394 | `b501cb8743e6c67b504cf194ec0bdbcaf21a1c0951a0f28cc82516d743ba1ecb` |

Nuspec licenses are MIT. The managed package contains a net10.0 target and depends on Microsoft.Extensions.AI.Abstractions 10.2.0. Runtime's NuGet dependency includes Runtime.Metal, though Windows dispatch should select CPU; ensure publish output contains only the intended architecture's critical native path. Do not choose AllRuntimes and unintentionally ship GPU dependencies.

The actual NoAvx package has Windows x64 DLLs and **no win-arm64 entries**, despite broadly worded online documentation. For older x64 CPUs, this package provides an explicit alternative to the normal runtime's AVX/AVX2/FMA/F16C requirement. ARM64 [toolchain source at 1.9.1](https://github.com/sandrohanea/whisper.net/blob/1.9.1/cmake/arm64-windows-llvm.cmake) specifies `-march=armv8.7-a`; document a corresponding minimum CPU or rebuild a broader target. Native ARM64 loading and sample inference remain necessary on this Snapdragon.

Useful pinned implementation references: [basic transcription](https://github.com/sandrohanea/whisper.net/blob/1.9.1/examples/Simple/Program.cs), [NAudio 16 kHz conversion](https://github.com/sandrohanea/whisper.net/blob/1.9.1/examples/NAudioResampleWav/Program.cs), [Silero speech detection](https://github.com/sandrohanea/whisper.net/blob/1.9.1/examples/Vad/Program.cs). The samples use WhisperFactory/CreateBuilder/ProcessAsync and WhisperVadFactory/CreateBuilder/DetectSpeechAsync. Use the project's verified downloader instead of copying the examples' unchecked download behavior. Avoid transcript-bearing debug logs.
