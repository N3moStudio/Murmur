# Murmur for Mac — initial source preview

**Status: implemented source, not yet compiled or run on a Mac.** Started November 5, 2026 after the tested Windows 0.1.3 update. This is not a finished Mac release or Windows feature parity. The current PC has neither Swift/Xcode nor a Mac SDK. No Mac latency, accuracy, RAM or battery claims have been measured.

Confirmed target: **Apple silicon, macOS 26+, Xcode 26+ / Swift 6.2**. Intel/older-macOS support would need a different speech backend. No third-party package dependency or cloud account is required for this preview. Apple's English speech assets download once through system-managed AssetInventory, then recognition is on device. Availability is checked on the actual Mac.

## First implemented slice

- Native AppKit menu-bar app and setup, with separate English download/microphone, Accessibility and shortcut controls.
- Double Caps Lock starts, one tap finishes, Escape cancels. Modified chords pass through. The event tap is registered only after setup and enabled permissions.
- SpeechAnalyzer/SpeechTranscriber consume live converted audio on an actor. The microphone opens only for an explicitly started dictation and is closed on stop/cancel. Audio capture starts before analyzer warm-up so initial buffered speech is retained. The pending audio queue is bounded; overflow fails visibly and retains its recording instead of silently dropping speech.
- Time-ranged speech results replace interim phrases and retain the final tail. Complete transcription waits for analyzer finalization. English is the only configured language.
- A nonactivating, mouse-transparent recording pill on the current screen. Elsepin Thinking Orbs' licensed `searching-20` construction is drawn natively. Animation runs only while visible, at 30 fps / 15 fps in Low Power Mode; Reduce Motion or the animation setting disables it.
- Stop-time destination capture and late editor selection. Returning to the same unchanged text field refreshes its caret. Secure fields are excluded; changed text, another target or unconfirmed writes preserve recovery instead of automatically retrying.
- Email recognition from the active app/window title, with conservative paragraph breaks for greetings/closings actually spoken. This first Mac slice does **not** include Windows' local AI cleanup, filler removal, correction model, dictionary, snippets or meeting tools.
- Transcript saved before attempted insertion. Failed/unavailable insertion keeps text plus a CAF recording in `~/Library/Application Support/N3moStudio/Murmur`. Audio is removed after a confirmed insert and saved history update. Cancelled/interrupted recordings remain there for manual recovery. Last transcript is selectable; saved JSON files remain accessible after restart. Recovery re-transcription, retention controls and a full history browser are still to be added.

## Build on a Mac

From the repository root:

```sh
bash macos/scripts/build.sh
open macos/.build/bundle/Murmur.app
```

The script runs nine core tests, builds the native executable, creates an app bundle and ad-hoc signs it for local development. `DEVELOPER_ID` optionally selects an existing signing identity; signing alone is not notarization. No certificate, paid service or release hosting has been configured. Do not distribute this as a verified installer.

First run: download English and approve the microphone, allow Murmur under Privacy & Security → Accessibility, and enable the Caps Lock shortcut. Input Monitoring may also be required by the event tap. Reopen after granting permissions if macOS requests it. Keep the test build at a stable path; rebuilding/ad-hoc signing can require granting permissions again.

## Required Mac validation before release

1. Compile with Swift 6.2 strict concurrency and the macOS 26 SDK; resolve compiler diagnostics rather than weakening isolation. Run all nine tests, including exact upstream orb vectors, gesture consumption, long transcript tails and email fidelity.
2. Verify both physical Caps Lock events and the system Caps Lock state, hardware key delay, permission denial/revocation and modifier chords. No 50 ms startup claim is made. Confirm the pill does not activate its app or interrupt typing.
3. Exercise microphone warm/cold start, rapid start/stop/cancel, sleep, headset disconnect/reconnect, prolonged dictation and a full disk. Audio conversion and recovery writes currently execute on the serial audio tap; profile that path on real hardware before promising latency.
4. Check TextEdit, Mail, Safari/Chrome contenteditable, Electron and secure fields. AX-only insertion deliberately declines apps without writable selected text; a guarded clipboard fallback remains to be implemented. Never submit a message automatically.
5. Verify no mic, animation timer or speech session remains active while idle, and measure the system speech worker as well as the app. Check model eviction/availability, memory, first-packet time and end-to-end insertion time. Exercise all monitor/scaling modes and Reduce Motion.
6. Add a distinct app icon, recovery UI, settings retention controls, wider feature parity, Developer ID/notarization and an installable update path before a public release.

Windows stays independent under `src/Murmur`; this source does not change its shipped installers. Spanish and French are future language candidates, not implemented models.

## Sources and licenses

- Apple's [SpeechAnalyzer](https://developer.apple.com/documentation/speech/speechanalyzer), [SpeechTranscriber](https://developer.apple.com/documentation/speech/speechtranscriber), and [AssetInventory](https://developer.apple.com/documentation/speech/assetinventory) APIs, using macOS 26 APIs (no macOS 27-only capture helpers).
- Thinking Orbs 0.3.2, copyright 2026 Jakub Antalik, MIT: [license](../licenses/thinking-orbs-LICENSE). Numeric parity is checked against the same independently saved upstream fixture as the Windows port. The license is copied into the app bundle.
