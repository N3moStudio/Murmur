# Murmur 0.1.3 — less repeated work, safer cleanup

Native Windows ARM64 and x64 update following the [free-alternative comparison](alternatives-comparison.md).

- Reuse decoded audio only when the exact float PCM, sample count and speech model identity match. Changes force a fresh decode. The cache stores text and hashes, not PCM, is bounded to 128 entries, and clears when a new recording starts or the engine unloads. The full final recording is still scanned and every uncached section is decoded.
- Cleanup now protects greetings/thanks and negation as well as names/numbers/links. A rejected section keeps its own original text; that safe fallback can be reused without another failed rewrite. It does not discard other clean sections.
- Individual model arrows, Download all below the three components, readable progress, stop-time editor selection and same-editor return from 0.1.2 remain.

On the same 83.35-second prepared synthetic fixture, final recognition was 347 ms and email cleanup 1,955 ms: 2,306 ms final wait versus 4,342 ms in 0.1.2. Raw recognition matched exactly; the final guarded run added no extra greeting/thanks and returned no warning. This is a file-based preparation measurement, not physical hotkey/microphone latency.

64 core checks pass, including exact-sample changes, different model/length, cache eviction/clear, invented greetings/repeated thanks and negation/contraction preservation. Live insertion was not exercised while the user studied. Full comparison methodology and limits are in the linked report.

The exact-audio-key mutation was detected by the new test; restored code passes all 64. Native ARM64/x64 builds and all three installers succeeded. Architecture inventory: 505 ARM64 and 523 x64 binaries, no mismatch. The automatic installer updated this PC quietly, preserved stored document counts and matched published payload hashes. A pre-upgrade SQLite backup was saved.
