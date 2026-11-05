# Murmur 0.1.2 — setup, focus and long dictation

Local update, November 4, 2026 (America/Los_Angeles). Work stayed within the Windows version. Spanish and French are provisional future-language choices; no Mac implementation or additional language models were added.

## Setup review and fixes

The review used source inspection, the production JavaScript/CSS with a synthetic native-message bridge, and invisible browser layout measurements. No screenshots, microphone capture, or desktop input were used for this update's verification.

| Before / finding | After | Reason / source |
|---|---|---|
| Broken: downward arrows were plain text, so a component could not be downloaded individually | Real labeled, keyboard-accessible download buttons | `web/model-setup.js`; each button requests exactly its component |
| Ugly/fragile: details and live download status shared a flex row | Two-column details/action grid; progress occupies a separate full-width row | `web/reference-layout.css`; fixed 44 px action, shrinkable text column and wrapping status |
| Bulk action appeared above the rows in settings, with a different setup layout | Download all sits below the third component in both locations | A single shared renderer keeps first-run and settings consistent |
| Clicking multiple download actions could collide with the native single-transfer gate | One queue deduplicates overlapping clicks, skips installed components and keeps working after one failure | `ModelDownloads`; pause clears queued work and cancels the active transfer, resume retains native partial-file support |
| Progress assumed a positive total, and error text had no dedicated layout | Indeterminate startup, bounded percentages, tabular counts and fully wrapping escaped errors | Tested zero/unknown totals, out-of-range bytes, long connection errors, failure, pause, installed and empty states |

The three names and file sizes come from a fixed catalog (at most three rows, largest writing component about 1.12 GB). Download state is an enum; received/total bytes are numeric; the native error message is unbounded. The worst-case fixture combines 100% progress, a realistic long URL-bearing error and a queued component. No arbitrary long model names or huge lists were fabricated. Existing theme tokens, native keyboard focus outlines and safe HTML escaping were retained. No design decisions remain awaiting approval.

The dev-only fixture is `tests/Web/setup-fixture.html`, with Demo/Worst/Empty/Partial/Ready cases; it is not included in the installed app. `tests/Web/check-layout.js` checks row overflow, text/action intersection, progress separation, minimum button size and footer placement. Onboarding and Settings each passed 16 combinations: widths 320/480/860/1440, light/dark, normal/double zoom (**32 total**). This is geometry/DOM verification, not a claim of a visual screenshot review.

## Dictation destination

Ordinary dictation now captures the focused window/field when recording stops, allowing the user to click into an editor while speaking. If no editable destination was selected, a focused editor can be selected before insertion. Returning to the same unchanged editor can refresh the caret/selection; changing the field or its text after stop retains the result for recovery. Voice commands retain their original selected text. A generic read-only web document is no longer treated as an editor merely because its UI Automation type is Document.

The editing-state check uses Microsoft's [TextPattern.IsReadOnlyAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.textpattern.isreadonlyattribute?view=windowsdesktop-10.0). Focus rules are tested as pure data policies. Actual cross-app insertion and physical clicking were deliberately not rerun while the user was studying; 0.1.1 native/WebView2 live-test evidence remains historical, not evidence that the new flow was exercised live.

## Long dictation

Cleanup no longer depends on an exact match of one entire previously prepared transcript. It splits long text into bounded sections, caches only sections that pass the protected-detail check, and reuses exact section/prompt matches after full final speech recognition. Dictionary/snippet rules and final protected-detail validation remain. Context changes invalidate the relevant cleanup reuse. The cache is bounded to 128 entries, cleared for a new recording and when the writing engine unloads.

Preparation is throttled to at least eight seconds between attempts. Natural pauses can trigger it, and continuous speech can trigger it every twenty seconds. The previous sixty-second preparation cutoff is gone. Microphones still close while idle. No speculative output is pasted.

Every cleanup section is retained, including the tail. A native writing response whose finish reason is `length` is rejected instead of accepting a truncated response; cleanup falls back to the original transcript on failure.

### Measured result

On this ARM64 PC, the same **83.35-second / 256-word synthetic speech fixture**, with warmed engines:

| Path | Speech | Cleanup remaining after stop | Total final wait |
|---|---:|---:|---:|
| Installed 0.1.1, full cleanup at end | 2.335 s | 6.124 s | 8.470 s |
| 0.1.2, sections prepared from 20/40/60/80-second audio prefixes | 2.342 s | 1.996 s | 4.342 s |

All four preparation passes and the final pass had no protected-detail warning. Distinct content anchors across the beginning, middle and final reminder survived, including the blue folder at reception and the closing. This measures an already-prepared background path with synthetic audio, not physical microphone timing or a guarantee for all speech. Preparation shifts work into recording time; it does not eliminate compute cost. Recognition is still rerun over the complete recording before insertion.

## Checks

- **60/60 core checks**, including late destination, same-field return/new caret, changed-field/text protections, long section reconstruction, stable-prefix reuse and numeric-correction boundaries.
- A deliberate mutation of the unchanged-text comparison failed three focus tests; restored code passed all 60.
- **9/9 JavaScript download tests**: individual/all requests, duplicate clicks, installed skips, failure/retry, pause/resume, zero totals and escaping.
- **14/14 native service checks**, including real loopback HTTP download resume, hash failure and cancellation, plus existing storage/provider/MCP checks.
- Actual local writing engine rejected an intentionally one-token-limited response; no partial output was accepted.
- Native ARM64 and x64 self-contained builds passed; architecture inventory found no wrong-architecture binaries. Physical x64 remains untested.

Evidence is in ignored `artifacts/*0.1.2*` and `artifacts/long-dictation-0.1.2/`. Test audio, synthetic result files and local browser profiles are excluded from installers and source archives.

## Local delivery

The automatic installer updated this PC successfully to 0.1.2. A pre-upgrade database backup was created. Stored document counts were preserved, and six installed payload files matched the published ARM64 build by hash. Murmur restarted in the background with no main window. ARM64, x64 and automatic installers are included in the delivery folder with SHA-256 checksums.
