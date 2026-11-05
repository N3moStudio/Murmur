# Existing dictation apps: bounded reuse review

Research date: **2026-11-04, America/Los_Angeles**. Scope: OpenWhispr plus one Windows-native alternative, TypeWhisper. Repositories were downloaded for source review. No application binaries, installers, install hooks or development servers were executed. Twenty selected pure OpenWhispr source tests were run under Node; no microphone, clipboard, desktop selection, cloud account or network model was used by those tests.

This research supplements the Wispr Flow parity checklist. Neither alternative defines a smaller feature scope, proves native Windows ARM64 support, or replaces direct comparison with Wispr's Windows interface.

## Decision

**Use narrowly selected OpenWhispr MIT modules and tests where they improve correctness, with attribution. Keep Murmur's architecture, native ARM64 implementation and Wispr-derived UI.** The strongest immediate candidate is its pure application-category classifier. Its selected-text edit response parser and tests are also useful. Do not rebase onto either complete app: both current Windows distributions target x64, and their own designs and service dependencies differ from the requested product.

## Identity and licenses

| Project | Inspected revision | Latest stable release verified through GitHub API | License | Windows architecture finding |
|---|---|---|---|---|
| [OpenWhispr](https://github.com/OpenWhispr/openwhispr) | `d5821a7c88f4c1803b06c51b4580f16c51dd0dbe`, main, package 1.11.0 | [v1.11.0](https://github.com/OpenWhispr/openwhispr/releases/tag/v1.11.0), published 2026-10-29 | [MIT](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/LICENSE), copyright 2024 OpenWhispr Team | Windows CI and native downloads target x64. ARM64 release assets inspected were Mac, not Windows. |
| [TypeWhisper Windows](https://github.com/TypeWhisper/typewhisper-win) | `12f53a4987d24a2e1760305c8a0cf547bfbbf24c`, main (1.3 development) | [v1.2.2](https://github.com/TypeWhisper/typewhisper-win/releases/tag/v1.2.2), published 2026-11-03 | [GPLv3](https://github.com/TypeWhisper/typewhisper-win/blob/12f53a4987d24a2e1760305c8a0cf547bfbbf24c/LICENSE); separate commercial licensing offered | Current README explicitly says x64 only and ARM emulation untested. The stable asset list contains win-x64 packages only. Older ARM64 claims do not describe the current release. |

Main-branch findings are not all claims about stable release binaries. No PE inspection or real app acceptance was performed for these alternatives. Their bundled models, fonts, icons and dependencies have their own terms; root MIT does not automatically license every third-party asset. Murmur should retain its own icon/identity and model-license review.

## Concrete OpenWhispr candidates

Unmodified selected files, MIT license and file SHA-256 values are saved in:

`research/remakes/reuse-candidates/openwhispr/`

`PROVENANCE.json` binds each file to the exact GitHub revision. Full read-only source checkouts remain under `research/remakes/openwhispr` and `research/remakes/typewhisper-win`. No candidate has been integrated into app source by this research pass.

### 1. Focused-app and browser-page classification — highest priority

[toneAppCategories.js](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/helpers/toneAppCategories.js) and [its tests](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/test/helpers/toneAppCategories.test.js).

Dependencies: none; pure CommonJS JavaScript, accepting `{ appSignature, windowTitle, urlHost }` and returning personal/work/email/other. Windows input is executable name plus window class. Seven source tests passed, exercising many more table cases.

Useful protections:

- Matches whole identity tokens, not arbitrary substrings: `Mailchimp`, `Teamspeak` and `Slackware Installer` remain unknown.
- Strips `Chrome_WidgetWin_N` before identity matching: an Electron app is not classified as a browser merely because it has Chromium's window class.
- Reads title-based site identity only for known browser processes or the Windows UWP frame host.
- Uses the last meaningful title segment, removes a recognized browser suffix/profile, handles Edge's multiple-tab wording and zero-width characters.
- Recognizes Gmail/Outlook/Slack/Teams/WhatsApp titles without treating a search result or a document merely mentioning those brands as that service.
- Optional host matching requires the exact host or a dot-delimited subdomain. `notslack.com` does not count as Slack.
- Native application identity wins over arbitrary text in its document title.

**Recommended integration:** port or reuse the classifier with its tests, map `other` to Murmur's existing document/other decision only after higher-confidence matches, and preserve explicit user overrides. Add Murmur tests for Thorium Gmail, Outlook subject/body fields, Google Docs titles containing Gmail, Slackware, Teamspeak, browser profiles, and unknown/localized titles. A generic field label such as “Message” or “Subject” should not by itself override strong application identity. Keep an unknown result rather than force a guessed category.

### 2. Capture context once, retain only the category for processing

[selectionManager.js](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/helpers/selectionManager.js), approximately lines 126–233, and [ipcHandlers.js](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/helpers/ipcHandlers.js), approximately lines 1830–1851.

Windows captures the foreground target through its native helper when recording is triggered, deduplicates a pending/recent capture, and reads classification while speech is being recorded. Window titles stay in main-process storage; the renderer gets only a category. A new capture supersedes an older pending one. **Windows does not provide a browser URL host in this inspected path**; host input is supported by the pure classifier but obtained through the Mac-specific context path.

Murmur can improve recognition with no continuous polling or other-window scan: snapshot the already-intended foreground target on the recording shortcut, classify that snapshot locally, and keep it tied to the recording ID. Record only the resulting category/confidence in normal diagnostics, not the user's window title/email/account text. Do not reclassify against the newly focused app at processing completion. Reuse the same bounded capture for selection/insertion rather than spawn repeated probes.

The whole selection manager is not a drop-in candidate: it depends on Electron, native helpers, clipboard coordination, platform paths and upstream target/session policy. Use the small algorithm and tests with Murmur's existing Windows integration.

### 3. Context-specific cleanup prompts

[toneStyle.ts](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/config/toneStyle.ts), [prompt rules](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/config/prompts/toneStyle.ts) and [integration tests](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/test/services/toneStyleCleanup.test.js).

Prompt rules depend only on the sibling tone types/constants. They add the output medium and selected tone after base cleanup/dictionary instructions. Email rules preserve dictated greeting/paragraph/signoff layout without inventing them. They require exact names/acronyms/dictionary spelling, and prohibit restyling code/commands. Chat can reduce punctuation; style is separate from app classification. Upstream defaults personal messaging to casual, whereas the current Wispr Windows docs default all categories to formal: preserve the user's parity requirement, not an upstream default.

The full upstream integration tests need Vite, renderer stubs and upstream stores/providers; they were reviewed but not run. Port focused prompt-contract cases into Murmur's own tests. Prompt instructions are useful but are **not** proof that a local model preserves all details.

### 4. Selected-text command safety and response parsing

[selectionEditing.js](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/helpers/selectionEditing.js) and [tests](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/test/helpers/selectionEditing.test.js).

Dependencies: none; ES-module JavaScript. Seven source tests passed. It separates the spoken instruction from quoted document data, treats document instructions as inert, asks the model to preserve untargeted text, and uses a one-field JSON replacement contract for local inference. Parsing rejects duplicate fields, extra keys, wrappers/trailing text and empty edits without changing decoded document whitespace. Questions about selected text return a separate routing marker instead of overwriting the selection with an answer.

**Reuse with care:** route explanation/question responses to an answer/review surface, not the insertion path. Preserve Murmur's stricter focus and consent checks. An instruction to delete all selected text requires an explicit operation contract because upstream rejects every empty replacement. Upstream “capture unavailable” can fall back to standalone assistant behavior; Murmur should not infer an edit succeeded when selection capture failed.

### 5. Preserve details beyond prompts

[English cleanup prompt](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/locales/en/prompts.json) distinguishes dictated commands/questions from instructions to the assistant. [cleanupOutput.ts](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/utils/cleanupOutput.ts) rejects a narrow repeated-whole-transcript failure using Unicode word segmentation. Its dependency is `src/utils/logger`; its tests need TypeScript loading and that module.

The bounded inspection did **not** establish a general deterministic protected-number/URL/email/code validator in OpenWhispr. Its selection edit parser validates the response envelope, not semantic fidelity. Do not replace Murmur's protected-details checks with these prompts. Retain originals and fail to a review/copy path if a cleanup unexpectedly changes protected values. Spoken corrections need explicit, targeted handling: a blanket “every number must survive” rule would wrongly reject “Thursday, no Friday” or a corrected amount. Benchmark separate cleanup and command cases using real models.

### 6. Windows paste targeting — reference, not blanket adoption

[windowsPasteTarget.js](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/src/helpers/windowsPasteTarget.js) is pure and its six tests passed. It handles HWND representation and excludes the tray owner's non-editor surface. [windows-fast-paste.c](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/resources/windows-fast-paste.c) is a Win32 helper with terminal detection, SendInput and foreground restoration.

Upstream can restore the original window; some paths can fall back to current focus if restoration fails. That is not necessarily the user's desired behavior. Murmur should keep its own strict target validation, clipboard sequence/ownership preservation and copy/retry recovery. Do not copy terminal or modifier behavior without testing the real target and keyboard layout. These helpers' published upstream artifacts are x64; source portability does not constitute ARM64 runtime evidence.

## Meeting and integration coverage in OpenWhispr

The current source contains substantial real implementations; this source inspection is not runtime verification:

| Area | Source evidence | Reuse value and remaining requirement |
|---|---|---|
| Meeting microphone and system capture | `resources/windows-system-audio-helper.c`, `meetingRecordingSession.ts`, `meetingAudioTimeline.js`, `meetingSystemAudioWatchdog.js` | WASAPI process-loopback helper, watchdog and bounded silence repair after explicit capture restarts. Cross-compile/PE-check/native ARM64 testing still required. Upstream captures different system-audio semantics than Wispr's default-output path. |
| Silence/echo gating | `meetingMicGate.js`, corresponding tests | Pure RMS/peak decisions; thresholds are product heuristics, not universal VAD quality. Rebenchmark for Murmur microphones and avoid dropping quiet speech. |
| Speaker diarization | `diarization.js`, `diarizationPolicy.js`, `meetingDiarizationInput.js` | Real sherpa/ONNX-based pipeline and clustering policy. Source explicitly acknowledges heuristics can drop a brief real participant. Two tracks alone are not diarization. Windows ARM64 engine/model and native dependencies remain unproven. |
| Meeting notes and summaries | `builtinActions.js`, `templatePrompts.js`, notes components | Detailed prompt structures, owner/action distinctions and preservation of exact entities. Keep typed notes separate; do not equate prompts with successful real summary generation. |
| Calendar | `googleCalendarOAuth.js`, `microsoftCalendarOAuth.js`, managers/reminder scheduler | Loopback PKCE flow and calendar caches are useful design references. Requires our own OAuth application identity, credentials/configuration and actual account authorization; do not reuse their client keys. |
| Folders/shared workspaces | `FoldersService.ts`, local folder/tool/store modules | Local structures exist; cloud service calls `/api/folders/*` and team scopes require backend authorization/sync. Merely copying the client does not deliver working sharing. |
| Questions over notes | services/tools and notes chat | Tool-based note retrieval and response surfaces are present. Needs integrated local inference/retrieval and verified citations; do not fabricate account or meeting answers. |

No borrowed feature may be considered done until tested in Murmur. Cloud workspace, calendar, sharing and sync dependencies remain explicit even if upstream has a corresponding screen.

## Architecture evidence

At pinned OpenWhispr revision:

- [Windows release job](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/.github/workflows/release.yml) uses `windows-latest`, x64-specific npm binaries and a Windows x64 binary cache. The x64/ARM64 matrix later in that file is **macOS**.
- [Whisper downloader](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/scripts/download-whisper-cpp.js), [llama downloader](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/scripts/download-llama-server.js), [sherpa downloader](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/scripts/download-sherpa-onnx.js), and Windows system-audio downloader have Windows x64 entries, not Windows ARM64 entries.
- [Electron packaging](https://github.com/OpenWhispr/openwhispr/blob/d5821a7c88f4c1803b06c51b4580f16c51dd0dbe/electron-builder.json) targets NSIS on Windows and includes architecture-sensitive native modules, inference/audio helpers and third-party resources. Upstream signing configuration is their identity and cannot be reused.

Murmur must independently provide native ARM64 inference, audio, UI runtime, helper processes and an architecture-aware installer. Reusing pure JavaScript does not resolve native dependency compatibility.

## TypeWhisper as a secondary reference

The [capability map](https://github.com/TypeWhisper/typewhisper-win/blob/12f53a4987d24a2e1760305c8a0cf547bfbbf24c/docs/WINUI-FUNCTIONAL-STATUS.md) explicitly distinguishes microphone/system recording from unavailable meeting automation/calendar sign-in. It has app/site workflow matching, a plugin system, local history, protected persistence and a local API, but is not a complete substitute for the current Wispr meeting/shared-library scope.

Useful inspected examples are `ClipboardPasteOperation.cs` and its tests (clipboard lease, focus recheck, partial SendInput and restoration), `WindowsClipboardTransaction.cs`, `InsertionContextReader.cs`, and `DictationInsertionText.cs` (Unicode-aware caret spacing and conservative final-period cleanup). They are **GPLv3**, not MIT. No TypeWhisper code was copied to the reuse-candidates directory. Do not port code into a differently licensed distribution without deliberately meeting that license's requirements; standard Windows behaviors can instead be implemented independently from platform documentation and our own tests.

Current TypeWhisper source uses .NET 10/WinUI 3 and a Windows SDK build floor. Its latest stable release has only x64 packages. This is useful evidence against assuming a C# codebase or earlier ARM64 release means current native ARM64 support.

## Verification performed

Command (from the pinned OpenWhispr source checkout):

```text
node --test test/helpers/toneAppCategories.test.js test/helpers/selectionEditing.test.js test/helpers/windowsPasteTarget.test.js
```

Result: **20 passed, 0 failed**, about 153 ms reported test-run duration. Node emitted an upstream module-format warning for the ES-module selection helper in a package without an explicit type; no source was changed to suppress it. This checks pure classifier/parser/target-policy fixtures, not model quality, Windows insertion, UI, microphone, app startup, source security, or physical hardware compatibility.

Next integration checks: preserve MIT notice for copied portions; add Murmur-specific foreground snapshots and manual override tests; run the classifier against labelled Windows contexts; validate local model style and protected details with actual transcripts; keep exact Wispr visual comparison and the full parity backlog separate from upstream reuse.
