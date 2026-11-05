# Two native CPU ASR candidates to benchmark

Checked 2026-11-04 against sherpa-onnx **2.0.1**, revision `af2d8d81dbfa61c4cea4c568da78f3982edca005`. No binary/model execution or application changes. Only source, metadata, licenses and small token text files were downloaded. The parent's measured baseline is Whisper.net base: 11-second JFK clip in 3.2 seconds; these alternatives have **not** been measured on this Snapdragon X X1-26-100.

**Benchmark Parakeet TDT v3 int8 first; Moonshine base-en quantized second.** Both use the already available native sherpa CPU runtime on ARM64/x64. Do not replace Whisper automatically until accuracy and end-to-end latency are measured. Model size is not a speed measurement.

| Candidate | Required files / total bytes | Coverage | Rationale and caveat |
|---|---|---|---|
| Parakeet TDT 0.6B v3 int8 | `encoder.int8.onnx`, `decoder.int8.onnx`, `joiner.int8.onnx`, `tokens.txt`; **670,478,772 bytes** | 25 European languages, automatic language detection | First candidate for throughput while retaining punctuation/capitalization; larger cold-load and resident-memory cost. Uses offline TDT, not true streaming in this sherpa export. |
| Moonshine base-en quantized (sherpa two-file “v2” package, 2026-02-27) | `encoder_model.ort`, `decoder_model_merged.ort`, `tokens.txt`; **141,300,566 bytes** | **English only** | Smaller CPU candidate for short dictation. Choose base rather than tiny initially to avoid assuming a quality reduction is acceptable. Do not confuse this package with the separate newer `moonshine-streaming-medium` architecture/benchmarks. |

Exact **immutable file URLs, sizes and SHA-256 hashes** are saved in [fast-asr-candidates.json](fast-asr-candidates.json). ONNX/ORT hashes are publisher HF LFS metadata; token hashes were computed from the downloaded text. Validate all actual model downloads before use.

- Parakeet conversion: [`csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` at `2bda32ec70b097a55adaa07d9a7173915b43cc78`](https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/tree/2bda32ec70b097a55adaa07d9a7173915b43cc78), ungated. [NVIDIA's source model card](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3/blob/541d1f99c6b0c3cd0b11a95167540bb8edefd82b/README.md) licenses weights under **CC-BY-4.0**. Attribute NVIDIA, link license/source and indicate ONNX/int8 conversion; do not claim the converter's code license replaces model terms.
- Moonshine conversion: [`csukuangfj2/sherpa-onnx-moonshine-base-en-quantized-2026-02-27` at `8f4d6c58c03d40bcea40043bb7120a878f2bbef6`](https://huggingface.co/csukuangfj2/sherpa-onnx-moonshine-base-en-quantized-2026-02-27/tree/8f4d6c58c03d40bcea40043bb7120a878f2bbef6), ungated. Its [included license](https://huggingface.co/csukuangfj2/sherpa-onnx-moonshine-base-en-quantized-2026-02-27/blob/8f4d6c58c03d40bcea40043bb7120a878f2bbef6/LICENSE) explicitly grants **MIT for English weights**, copyright 2025 Useful Sensors/Moonshine AI. Other language weights use separate non-commercial terms; English MIT must not be generalized to them. Full text is saved as `research/fast-asr-reference/moonshine-LICENSE`.

Parakeet supports bg, hr, cs, da, nl, en, et, fi, fr, de, el, hu, it, lv, lt, mt, pl, pt, ro, sk, sl, es, sv, ru, uk. Retain Whisper for languages outside the validated candidate's coverage. NVIDIA's GPU long-audio figures are not native CPU/ONNX memory limits; use VAD-bounded utterances and benchmark long dictation separately.

## Exact managed configuration

Sources: [OfflineRecognizerConfig](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/scripts/dotnet/OfflineRecognizerConfig.cs), [OfflineModelConfig](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/scripts/dotnet/OfflineModelConfig.cs), [official Moonshine v2 C# example](https://github.com/k2-fsa/sherpa-onnx/blob/af2d8d81dbfa61c4cea4c568da78f3982edca005/dotnet-examples/non-streaming-moonshine-v2-decode-files/Program.cs), [official Parakeet documentation](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/offline-transducer/nemo-transducer-models.html).

Parakeet configuration sketch:

```csharp
var config = new OfflineRecognizerConfig();
config.FeatConfig.SampleRate = 16000;
config.FeatConfig.FeatureDim = 128;
config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, "encoder.int8.onnx");
config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, "decoder.int8.onnx");
config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, "joiner.int8.onnx");
config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
config.ModelConfig.ModelType = "nemo_transducer";
config.ModelConfig.Provider = "cpu";
config.ModelConfig.NumThreads = 2;
config.DecodingMethod = "greedy_search";
```

Moonshine configuration sketch (new config; leave other model families empty):

```csharp
var config = new OfflineRecognizerConfig();
config.ModelConfig.Moonshine.Encoder = Path.Combine(modelDir, "encoder_model.ort");
config.ModelConfig.Moonshine.MergedDecoder = Path.Combine(modelDir, "decoder_model_merged.ort");
config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
config.ModelConfig.Provider = "cpu";
config.ModelConfig.NumThreads = 2;
config.DecodingMethod = "greedy_search";
```

Processing API: `new OfflineRecognizer(config)`; `recognizer.CreateStream()`; `stream.AcceptWaveform(16000, monoFloatSamples)`; `recognizer.Decode(stream)`; `stream.Result.Text`. Retain recognizer/model between utterances when measuring warm decode; use a fresh stream per utterance. `OfflineRecognizer` is IDisposable. The managed constructor does not check a null native handle, so retain the checked P/Invoke/isolated-worker approach already selected. Decode is synchronous; do not dispose while active. Native structs must match the full pinned 2.0.1 layout, not a shortened family-specific structure.

No extra runtime package is needed beyond the already integrated architecture-specific sherpa native package. See [diarization research](diarization-research.md) for precise runtime graph and versions. Adding the managed package would restore nine platform runtime dependencies; existing checked bindings can instead expose the OfflineRecognizer C API.

## Benchmark decision

1. Test **JFK + the existing multi-speaker/numbers fixtures**, and a small real dictation sample if available. Add quiet/noise-only controls and a short yes/no utterance. Synthetic speech alone cannot establish accent, noisy-room or spontaneous-speech accuracy.
2. Separate cold model load, first decode, warmed decode and final insertion latency. Compare 2 and 4 inference threads first; expand only if results justify it. Run engines sequentially to avoid CPU/memory contention. Record peak working set and final output text.
3. Score lexical errors independently from punctuation/case. For numbers, dates, currency, names and negations, check exact intended meaning; do not count merely formatting “twenty” as “20” as a lexical regression, or normalize away an incorrect amount.
4. Benchmark raw ASR before cleanup. Parakeet already produces punctuation/case, so unnecessary cleanup may dominate the final latency even if ASR improves. Keep any cleanup policy change explicit and retain original text.
5. Promote a faster candidate only if it preserves or improves the important fixture outcomes. Otherwise leave it as an explicit speed option and keep the existing quality fallback. Silence/VAD protection remains necessary with either model.

SenseVoice was not selected as a third benchmark: its current original [model card](https://huggingface.co/FunAudioLLM/SenseVoiceSmall/tree/3847d57b6bdf2dd8875cb1508d2af43d80a16bf7) identifies a custom model license, and another candidate would broaden this bounded comparison without first measuring the two straightforward options. This is not a finding that SenseVoice is unusable or slower. No external benchmark has been treated as evidence of Snapdragon Windows performance.
