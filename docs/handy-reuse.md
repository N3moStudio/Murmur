# Handy source research for Murmur

Read-only investigation on 2026-11-04. No Handy executable was downloaded or run, no application source was changed, and no user/private material was inspected. These findings are source/package/build evidence, not local performance or delivery verification.

## Revision, license and reusable scope

Official repository: [cjpais/Handy](https://github.com/cjpais/Handy). Latest release observed: [v0.10.0](https://github.com/cjpais/Handy/releases/tag/v0.10.0), published 2026-11-02 13:13:42 UTC. Release/main resolved to **82dfab9fda18c4bc24e1f6e56d36474d38eca0bc** (2026-11-02 12:40:42 UTC).

[LICENSE at this revision](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/LICENSE) is **MIT, copyright 2025 CJ Pais**. Source adaptation is permitted subject to retaining the copyright and permission notice in copies/substantial portions. This does not automatically license models or every transitive dependency. Preserve attribution when adapting source, including a substantial translation to C#. No Handy implementation has been copied into Murmur by this investigation.

Relevant exact Cargo.lock packages, checked against actual crates.io source archives:

| Dependency | Version / source revision | License | Archive SHA-256 |
|---|---|---|---|
| handy-keys | 0.3.4 / e7d1b8e6de8258f21ee1d60608d16f8d195157bf | MIT | 1a007b6c921d3273fd88aac45b516eee01e5a9dc2d76bb6f1a981500cc0d818a |
| handy-recorder | 0.1.0 / 1d975fe8c10f9a69d778cf16955c7e0699b4b8e1 | MIT | 4a734d6d16eb8eb485548d75f383fa0c09d65b037a20e648c19730860f7a8a3f |
| transcribe-cpp | 0.4.0 / f1dcd15858c4bbba87ca97862b4e448b2cf15b8f | MIT | f6cad85eeb846a7064891c8b49b92a57a0f2d4dd089104cb199608b23043555e |

Primary package sources: [keys](https://crates.io/crates/handy-keys/0.3.4), [recorder](https://crates.io/crates/handy-recorder/0.1.0), [transcribe](https://crates.io/crates/transcribe-cpp/0.4.0). Source provenance is recorded locally under `research/handy-reference/source-manifest.json`; the dependencies retain their license files and `.cargo_vcs_info.json`.

## Native Windows evidence

[Release workflow](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/.github/workflows/release.yml#L63) targets `x86_64-pc-windows-msvc` on `windows-latest` and **`aarch64-pc-windows-msvc` on `windows-11-arm`**. The release API lists `Handy_0.10.0_arm64-setup.exe` (10,757,896 bytes), `Handy_0.10.0_arm64_en-US.msi` (15,245,312), and x64 equivalents (17,247,944 / 32,698,368). This is explicitly ARM64, not ARM64EC or x64 emulation. PE headers were not inspected here.

[Cargo.toml](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/Cargo.toml#L126) selects static CPU-only transcribe-cpp on Windows ARM64; x64 selects dynamic CPU ISA variants and Vulkan. All ASR models now use transcribe-cpp; older descriptions of Handy's ONNX inference stack are stale for this revision. Do not infer NPU or Adreno acceleration from its ARM64 installer.

[Build workflow](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/.github/workflows/build.yml#L258) uses clang-cl/Ninja for ARM64, pins `GGML_NATIVE=OFF` and **`GGML_OPENMP=OFF`**, and bundles redistributable VC runtime DLLs. x64 also stages `vcomp140.dll`. It requires packaged ARM64 device enumeration to show CPU and no GPU. These are workflow checks, not checks run by this investigation. This supports Murmur's existing native CPU route; switching stacks is unnecessary merely to obtain ARM64 support.

## Audio, VAD and memory lifecycle

[Audio manager](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/managers/audio.rs) requests mono 16 kHz frames. It caches device IDs but invalidates stale selections, can retry a failed start with a reopened device, and preserves the portion captured before a device failure. A generation token invalidates delayed first-sample UI/chimes after stop/cancel. On-demand microphone closure is the default; optional lazy closure waits 30 seconds and checks both generation and idle state before closing.

[handy-recorder engine](https://github.com/handy-computer/handy-recorder/blob/1d975fe8c10f9a69d778cf16955c7e0699b4b8e1/crates/handy-recorder/src/capture/engine.rs) separates device callbacks, delivery/sink work, and failure notification; its Windows backend is WASAPI. The recorder tracks first-audio failure, stalled callbacks, stalled delivery, suspension, and dropped audio. Default bounds include 10 seconds for no audio and 5 seconds for stalled callbacks/stop. Reusable principle for NAudio: keep ASR and expensive work outside the capture callback; report device failure separately from normal silence and preserve recoverable captured audio.

[Silero wrapper](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/audio_toolkit/vad/silero.rs) uses one CPU thread and resets recurrent state between recordings. Its **GGUF** Silero v6.2 model is not interchangeable with Murmur's whisper.cpp VAD `.bin` just because both say Silero. [Smoothing constants](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/audio_toolkit/vad/mod.rs#L5) are 450 ms preroll, 450 ms offline tail, 1,650 ms streaming tail and 60 ms onset; the audio manager uses threshold 0.3. These are tuning references, not validated settings for Whisper.net.

[Capture sink](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/audio_toolkit/audio/sink.rs) reuses one detector with per-session policy, distinguishes all-zero input from no input, and signals readiness on the first real callback. It keeps whole VAD frames, including a padded final partial frame. A VAD exception currently passes that frame through; copying that fallback blindly would weaken Murmur's silence-hallucination protection. Keep short-word and quiet-speech tests when choosing padding/minimum speech durations.

[Transcription manager](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/managers/transcription.rs#L304) checks idle unloading every 10 seconds and refreshes activity during recording/file jobs. [Settings](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/settings.rs#L150) default to five-minute model residency, with immediate/never alternatives. Unloading clears public model state synchronously and queues later loads behind teardown.

[ASR worker](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/engine_supervisor/worker.rs#L162) owns the ASR model/session in a separate process; parent stdin closure exits it, and cancellation kills it so the OS releases allocations even if native work hangs. [Supervisor](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/engine_supervisor/supervisor.rs) serializes work, uses cancellation epochs, and can recover GPU failure with CPU. Handy's VAD also uses transcribe-cpp inside the parent, so the worker comment saying it is the only native-code location is too broad. For Murmur's in-process Whisper.net path, cancellation must not dispose an active native call; discard obsolete results and dispose after completion, or explicitly introduce worker isolation later.

## Global shortcuts and modifier edge cases

[handy-keys Windows listener](https://github.com/handy-computer/handy-keys/blob/e7d1b8e6de8258f21ee1d60608d16f8d195157bf/src/platform/windows/listener.rs) uses low-level keyboard/mouse hooks on a message-loop thread. It tracks left/right modifiers, reconciles missed releases against `GetAsyncKeyState`, responds to session lock/unlock and resume, and replaces hooks before removing old ones. It avoids reconciliation during modifier callbacks because async state has not yet applied the in-flight event. It filters AltGr's synthetic Left Ctrl scancode so Right Alt does not accidentally trigger Ctrl bindings. Suppressed Win/Alt combinations inject a marked inert menu-mask event to prevent a stray Start menu/menu-bar activation; their own injections pass through without changing shortcut state.

[Key manager](https://github.com/handy-computer/handy-keys/blob/e7d1b8e6de8258f21ee1d60608d16f8d195157bf/src/manager.rs) tracks active bindings to ignore repeat presses and emits release when the main key lifts or modifiers no longer match. [Coordinator](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/transcription_coordinator.rs) models hold, toggle, and hold-or-toggle explicitly, preserves toggle parity while busy, debounces keyboard presses 30 ms, and gives release 50 ms grace for X11 repeat. The X11-specific delay need not be copied into Windows. [Shortcut setup](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/shortcut/mod.rs) falls back to the Tauri registration route if its preferred manager fails; modifier-only bindings require special handling rather than assuming RegisterHotKey supports them.

## Paste recovery and limits

[Output actions](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/actions.rs) keep original text if cleanup fails or produces blank output, recheck cancellation before paste, and save text/history before paste when audio history exists. A session counter prevents late paste completion from clearing a newer session's overlay. These are directly suitable ideas for Murmur's durable text recovery and cancel behaviour.

[Default clipboard path](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/clipboard.rs#L73) serializes pastes, supports Ctrl+V/Ctrl+Shift+V/Shift+Insert or direct typing, restores old clipboard content even on injection error, and runs delayed work outside the UI thread. Its normal restore is fixed-delay and only saves text or, if no text, an image. It is **not** a complete rich-format preservation implementation and has no sequence guard in that path.

[Receipt-based paste policy](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/paste_tx/mod.rs) is explicitly beta and **disabled by default**. [Windows implementation](https://github.com/cjpais/Handy/blob/82dfab9fda18c4bc24e1f6e56d36474d38eca0bc/src-tauri/src/paste_tx/windows.rs) publishes delayed-render Unicode text from a hidden message window, records reads after injection, waits 200 ms after the last read (8-second maximum; 500 ms after failed injection), and restores only if ownership/clipboard sequence is unchanged. It settles an older transaction before a newer snapshot. It attempts multiple clipboard formats and bitmap preservation but skips unsupported/oversized formats, including a 64 MiB per-format bound.

A clipboard read is evidence that *a consumer* read it, not proof that the intended editor inserted it. Another consumer can read after injection; delayed rendering ordinarily produces a receipt once data is materialized. The beta method can return success after chord injection failure while its worker restores later. Do not inherit its success wording as a delivery guarantee. No examined path establishes that elevated targets or arbitrary custom controls accept injected input. Murmur should retain recoverable text and clearly expose Copy/Retry when insertion cannot be confirmed; foreground checks and integrity restrictions still need Murmur-specific validation.

## Highest-value reuse for the current C# implementation

1. Preserve raw and cleaned text before attempting insertion; never replace useful text with blank cleanup output. Recheck cancellation/session generation before every external side effect.
2. Treat no input, all-zero input, no detected speech, device loss and inference failure as separate outcomes. Show recording readiness after actual audio arrives.
3. Keep capture callbacks cheap, cap queued work, retain partial recordings after device loss, and close idle devices/models without racing a new recording.
4. Add focused modifier cases: repeat, release order, AltGr, left/right keys, sleep/lock, and Win/Alt menu activation. Avoid doing expensive work in a low-level hook.
5. At minimum guard clipboard restoration with its sequence number, serialize paste transactions and preserve recovery text. Treat receipt-based restoration as an optional additional design, not a prerequisite or a proven guarantee.

Suggested verification cases for the main agent: quiet single-word utterance; noise-only recording; unplug/replug and sleep/resume; cancellation during native inference/cleanup; rapid start-stop-start; long held shortcut; both modifier release orders; user copying new content during paste; rich clipboard/image preservation; slow target; unavailable/elevated target; and retry after failed insertion. None were executed as part of this research.
