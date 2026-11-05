# Managed VAD API: Whisper.net 1.9.1

Inspected the actual `lib/net10.0/Whisper.net.xml` in the downloaded 1.9.1 NuGet package and the matching tagged source. Extracted API XML: `research/dependencies/Whisper.net-net10.xml`. Source evidence is retained in `research/dependencies/whisper-net-source/`. This is API/source verification, not a completed audio benchmark.

## Exact public surface

All types below are in namespace `Whisper.net`:

- `WhisperVadFactory.FromPath(string)` or `FromPath(string, WhisperFactoryOptions)` creates the factory.
- `CreateBuilder()` returns `WhisperVadProcessorBuilder`.
- Builder options: `WithThreads(int)`, `WithUseGpu(bool)`, `WithGpuDevice(int)`, `WithThreshold(float)`, `WithMinSpeechDuration(TimeSpan)`, `WithMinSilenceDuration(TimeSpan)`, `WithMaxSpeechDuration(TimeSpan)`, `WithSpeechPadding(TimeSpan)`, `WithSamplesOverlap(TimeSpan)`, then `Build()`.
- `WhisperVadProcessor.DetectSpeechAsync(Stream, CancellationToken)` accepts a WAV stream; overloads accept `float[]` and `ReadOnlyMemory<float>` samples.
- It returns `Task<IReadOnlyList<VadSegmentData>>`. Each segment has `TimeSpan Start` and `TimeSpan End`, already converted into seconds internally. Do not divide their values by 100 again.
- Sync `DetectSpeech` overloads also exist. `DetectSpeechNoReset` and corresponding async overloads preserve recurrent model state; normal `DetectSpeech` resets state for each recording. `ResetState()` is explicit.
- The processor implements `IAsyncDisposable` and `IDisposable`; the factory implements `IDisposable`.

Sources: [WhisperVadProcessor](https://github.com/sandrohanea/whisper.net/blob/1.9.1/Whisper.net/WhisperVadProcessor.cs), [builder](https://github.com/sandrohanea/whisper.net/blob/1.9.1/Whisper.net/WhisperVadProcessorBuilder.cs), [official Silero v6.2.0 example](https://github.com/sandrohanea/whisper.net/blob/1.9.1/examples/Vad/Program.cs).

## Candidate integration pattern

This is a proposed configuration, not a quality-validated preset. The official sample uses threshold 0.5. A 250 ms minimum can discard brief words, so validate “yes”, “no”, “stop” and short names before settling on a minimum. Start with 150–250 ms and tune against real quiet speech and noise samples.

```csharp
using var factory = WhisperVadFactory.FromPath(sileroModelPath);
await using var vad = factory.CreateBuilder()
    .WithUseGpu(false)
    .WithThreads(2)
    .WithThreshold(0.5f)
    .WithMinSpeechDuration(TimeSpan.FromMilliseconds(150))
    .WithMinSilenceDuration(TimeSpan.FromMilliseconds(100))
    .WithSpeechPadding(TimeSpan.FromMilliseconds(100))
    .Build();

var speech = await vad.DetectSpeechAsync(mono16kSamples, cancellationToken);
cancellationToken.ThrowIfCancellationRequested();
if (speech.Count == 0)
{
    // Finish with a clear no-speech result; do not invoke ASR or cleanup.
    return;
}
// Transcribe selected padded speech regions, or the original short clip.
// Keep source offsets if slicing audio so meeting timestamps stay correct.
```

Use mono 16 kHz float samples in [-1,1], or a supported 16 kHz WAV. This API has no sample-rate parameter on the sample-array overload, so feed the correct rate. The [NAudio example](https://github.com/sandrohanea/whisper.net/blob/1.9.1/examples/NAudioResampleWav/Program.cs) demonstrates 16 kHz resampling. Avoid repeatedly materializing long meeting recordings into multiple arrays; process bounded chunks while preserving source-time offsets. Separate recording jobs should use the resetting path.

The model is the already catalogued MIT Silero `ggml-silero-v6.2.0.bin`, 885,098 bytes, SHA-256 `2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987`. Do not substitute the ASR model in this factory. Verify its checksum before loading. Initialize the desired native CPU runtime policy before the first Whisper/VAD factory is created.

## Cancellation and lifetime caveat

Source inspection shows async detection runs on a long-running worker and serializes calls with a semaphore. The cancellation token gates scheduling and waiting for the semaphore, but **does not interrupt the native speech-detection call once underway**. Cancellation must also invalidate the job generation; check cancellation/generation after the await before any ASR, cleanup, history write, or insertion. For longer audio, bound chunk sizes so cancellation does not wait for an entire meeting.

Synchronous `Dispose()` throws while processing is active. Await `DisposeAsync()` to wait until the worker releases the context; do not free the context from a cancellation callback. This follows the actual 1.9.1 source, not an assumption based on the Async suffix. Native VAD failure throws `WhisperProcessingException`; model loading failure throws `WhisperModelLoadException`. Treat either as a visible failure/retry state, not silent approval to transcribe unfiltered noise.

## Silence and noise policy

VAD is one layer, not a proof that audio contains intended dictation. Background speech or music can trigger speech detection. Skip exact empty/zero recordings early, then use VAD; do not rely on an aggressive amplitude threshold that rejects quiet users. If no speech is detected, produce no transcript and no cleanup request. Do not pass an empty transcript into a generative model.

ASR builder additionally supports `WithNoSpeechThreshold(float)` (documented experimental, default 0.6), `WithLogProbThreshold(float)` (default -1.0) and `WithEntropyThreshold(float)` (default 2.4). These are decoding/fallback controls, not substitutes for VAD. Changing them requires noise and speech regression tests. Avoid deleting a stock list of phrases such as “thank you”; the user may actually have spoken them.

Required audio checks: digital silence, microphone room tone, fan/keyboard noise, brief quiet speech, clipped speech, pauses, TV/background voices and a second language. Confirm no insertion on no-speech/cancelled jobs. Report false positives and false negatives separately. This research did not run those checks.
