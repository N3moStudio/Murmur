# Murmur 0.1.1 — local release verification

Verified on this Windows 11 Snapdragon ARM64 PC, 2026-11-04/05. This update is not a claim of Wispr's proprietary accuracy or universal latency.

## Changes

| Before | After | Why |
|---|---|---|
| Ctrl double tap; microphone and target lookup started serially | Caps Lock twice starts; one tap stops. First key press initializes the audio client without capturing; confirmed gesture starts capture before app field inspection | Reduces activation delay and missed opening words |
| No-speech result discarded its audio | A failed history entry retains audio for playback/retry | Makes failures recoverable |
| Several speech model choices | One automatic English speech setup; separate speech detector and writing component | Removes model decisions while retaining email formatting |
| Browser field replacement blocked otherwise unchanged insertion | Accept exact same identified field, full text and caret after a runtime-ID change | Handles recreated text areas without loosening changed-field guards |
| Static orb on battery or with Windows animation disabled | 15 fps battery / 30 fps power; Animated, Follow Windows and Still choices | Gives visible processing feedback with an explicit motion preference |
| Cramped example chips beside the library buttons | Plain examples on a separate line with spacing | Makes Dictionary and Snippets legible |
| Custom maximize button without Windows hit testing | Native Windows caption controls; smaller minimum width | Restores the OS maximize/Snap surface and accommodates split layouts |
| Moon shown in both themes | Matching stroke moon in dark mode and sun in light mode | Shows the current theme |
| Unclear general versus category tone controls | Default tone, category explanations, and cleanup-dependency text | Explains which preferences affect each app |

## Measured checks

- Core rules/storage: **51/51** passed. A deliberate mutation of the recreated-field text comparison caused the expected regression test to fail; restored source passed.
- Owned native and WebView2 insertion fixtures: **46/46** passed. Unicode/multiline, selected-text replacement, no automatic submission, clipboard restoration/concurrent changes, cancellation, changed field/text/caret guards, closed target, recreated browser fields and nonactivating overlay.
- Native orb: **6/6** passed. Rendered frames change while processing; correct power frame cap; hidden timer stops; Still remains stationary; Follow Windows respects the actual disabled Windows animation setting; recording has no orb animation timer.
- Browser orb visual fixture: 11 additional painted frames in 800 ms with the battery cap enabled. Still produced no additional frames; removing the fixture left no scheduled animation. This is an instrumented component check, not a hardware frame-pacing measurement.
- Activation: five actual WASAPI runs with a synthetic Caps Lock sequence dispatched through the production hook handler. Popup paint **13–44 ms**, audio client Start returned **16–45 ms**, first audio packet **34–63 ms** after the completed gesture. A lone Caps Lock tap captured no audio; abandoned preparation and canceled startup released the microphone. These measurements use 50 ms key presses and a 90 ms gap; they are not a 50 ms guarantee for physical keys, other drivers or system load.
- Native ARM64 speech, warmed engine: 11-second human JFK sample **302 ms**; quiet version **236 ms**; short synthetic reply **75 ms**. Digital silence and synthetic fan noise returned no text. Samples are limited, not an accuracy benchmark.
- Synthetic 13.22-second email, warmed engines: **356 ms** transcription + **988 ms** writing polish, **1.35 s** total. The final result retained the date/time, resolved an explicit 37-to-38 correction, and formatted the spoken greeting/sign-off. The initial long prompt failed protected-detail validation; the original text was safely retained. A shorter prompt passed the actual engine fixture; the protected-detail guard was not weakened.
- Actual UI: Dictionary/Snippets spacing; Style and setup at 480 CSS px; moon-to-sun change; native maximize/restore controls; new motion preference; optional speaker setup. No horizontal overflow in the checked narrow Style view. Native Snap hover flyout was not directly observed because available native automation lacks hover.
- ARM64 and x64 self-contained publish passed. Architecture scan: **505 ARM64 / 523 x64 PE files**, no wrong-architecture payloads. Physical x64 remains untested.

## Scope and limitations

Email/chat classification uses the focused app/window/field and can misclassify unfamiliar apps. No inbox reading or sending occurs. Clipboard and focus guards may require copying from History in unsupported controls. Physical keyboard timing, device unplug/resume, physical x64, IME and multi-monitor layouts remain broader device-validation gaps. Microphones are not continuously recording in the background; first-tap initialization expires after 900 ms if no confirmed shortcut follows. The working orb follows Murmur's explicit preference without changing Windows settings.

Evidence is kept under ignored artifacts/: tests-0.1.1.log, mutation-0.1.1.log, windows-0.1.1.json, activation-release-0.1.1.json, overlay-motion-0.1.1.json, verify-0.1.1-*.json and ui-*-0.1.1.png. These test recordings and screenshots are not shipped.

## Installed update

The architecture-selecting installer exited successfully and installed native ARM64 (PE machine AA64). The installed executable, assembly and changed web assets match the published build hashes. Settings migrated from Double Ctrl to Double Caps Lock; OrbAnimation is on; English Moonshine remains selected. Stored document counts matched the pre-update database backup. Seven unused Whisper/Parakeet/speaker component files were moved to Windows Recycle Bin after automatic review blocked permanent deletion. The three-part dictation setup remains installed; optional speaker identification can be set up separately in Notetaker.

Started with --background: no main window, no WebView2 child process, no idle overlay. Across a 20.35-second process-tree sample: **70.6 MiB working set, 19.9 MiB private memory, 0.0768% total CPU** on eight logical processors. Active transcription/writing uses more memory and CPU; this is the idle measurement only.

All three installers were built: automatic architecture selector, ARM64 and x64. The local release remains unsigned. Installation and retained-data checks are recorded in artifacts/installed-0.1.1.json and artifacts/install-0.1.1.log.
