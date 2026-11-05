# Mac source preview — verification limits

November 5, 2026. This preview has not been compiled, launched or tested with a microphone on a Mac. This Windows host has no Swift/Xcode/macOS SDK. The Windows 0.1.3 checks do not validate Mac code.

Checks actually completed here:

- Both XML property lists parsed successfully, including microphone usage text and bundle identity.
- The build script passes Bash syntax checking. Its commands have not run on macOS.
- All Swift source is readable UTF-8; the nine proposed Swift Testing cases and the fixture are included in the package.
- The 54-dot fixture is byte-identical to the existing upstream reference. Executing the Swift numeric comparison is pending.
- Static control-flow review found and fixed a consumed-double-tap bug, cancellation during asynchronous startup, callbacks doing AX work before returning to the event tap, and accidental reuse of a previous transcript in a new failure record.
- Cancellation waits for the pending controller task before allowing another dictation; quit requests audio shutdown before application termination. These paths still require device tests.
- No desktop automation, screenshot or microphone capture was used for this work.

Still required: full Swift 6.2 build, all nine tests (plus an actual mutation check), platform permission behavior, physical Caps Lock state/latency, audio interruption and callback profiling, target-app insertion, UI layout/accessibility, idle/system-worker resource measurements, icon and release signing/notarization. See README.md for the test matrix and explicit missing Windows features.
