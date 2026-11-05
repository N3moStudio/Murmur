# Feature and UI parity checklist

Reference: installed Wispr Flow **1.6.1376**, package commit 656b3fbfe214db4026fff75b3db650fc3edec8bb. Investigated 2026-11-04 America/Los_Angeles on Windows 11 build 26200, physical ARM64, 16 GB. Original Electron app runs **emulated x64** here. Public docs may describe newer/server-enabled behavior.

Evidence: `reference-public.md` R01–R25 with official URLs; `evidence/rea-reference.json`; screenshots under `reference-images/`. REA ASAR SHA256 eebd5571b006bdb9e2e65449a6d838ffd3e6d9a2c6b277212c097dd38ab7f0f6; extraction ev_612c95277b5769ec5a2bddff940b831e43e1249ac2d0112a4d2121c59eefdaf4. Three unpacked native files contradict ASAR hashes; retained as observed-untrusted, not executed separately or copied. Installer is x64, file version 1.0.0.0, distinct from app version.

Implementation and verification are tracked separately below. English-only scope supersedes the original multilingual requirement. A screen existing does not establish functional parity. Remaining differences are listed explicitly; this is a local release, not a claim of identical proprietary output.

| Area | Original and evidence | Implementation | Verification required / uncertainty |
|---|---|---|---|
| Window/navigation | R01; observed Dictation, Notetaker, Insights, Dictionary, Snippets, Style, Transforms, Scratchpad; tray, close/min/max | Implemented native tray and local hub; named navigation surfaces, persistence and close-to-tray | Hub/CDP navigation and save/restart observed. Native activation later blocked by Windows lock screen. |
| Visual system | Runtime 1352×854 CSS px, DPR 1.25; Figtree 14px; shell #f5f4f0, selected #eeebe3; sidebar 216px, rows 36px; rounded white content | Reference geometry, Figtree, warm shell and sidebar, light/dark; licensed Elsepin Thinking Orbs | Matched home at equivalent CSS size after iteration. Artwork, icons, some settings/modal/editor details differ; exact paid-flow screenshots unavailable. |
| Onboarding | R04; existing reference onboarding observed, partial sequence | Five-step local welcome/models/mic/shortcuts/practice | Fresh-state screen and downloads observed. Account/subscription screens replaced with local setup. |
| Hotkeys | R02–03; modifiers, hold/release, double-tap lock, cancel, mouse, conflict detection | Configurable keyboard hold/toggle/command; double Caps Lock start, single Caps Lock stop; Escape; reserved/duplicate validation | Core rapid/repeat/chord tests pass. External hook conflicts cannot be fully discovered; mouse shortcuts not included. AltGr/IME and physical key sequence need further device checks. |
| Mic/audio | R11; device selection/test/hotplug | WASAPI device selection, 5-second test, levels, read-failure recovery, missing-packet detection | A real microphone test completed earlier. New no-packet guard reviewed. Physical unplug/exclusive device and sleep/resume remain unverified. |
| Overlay | R12; idle/recording/processing/result/failure, monitor docking | Native nonactivating pill; visible only recording/processing; 30 fps orb on power / 15 fps on battery; explicit Animated, Follow Windows and Still preferences | Native no-activation/hide checks passed; six rendered-motion, power-cap and preference checks passed with Windows animation disabled. Multiple-monitor/DPI testing outstanding. |
| Recognition | R03/R25; multilingual, real audio, silence | One automatic English Moonshine speech engine; Silero VAD; separate writing polish component | Real 11-second JFK, quiet speech and synthetic fixtures; silence/noise empty. Native ARM64 and emulated x64 final speech path executed; physical x64 not available. |
| Cleanup/style | R07–08; raw/light/medium and per-app categories | Local Qwen; protected literals; explicit corrections; email/chat/document classification and app overrides | Real engine samples and protected-number tests; context catalog golden tests. Unknown apps may need override. No proprietary-quality equivalence claimed. |
| Dictionary | R05; CRUD, correction pairs, import, personal/shared | CRUD, whole-word correction, search/import/export and selected shared entries | Core Unicode/overlap/literal tests and UI persistence. Moonshine uses post-recognition corrections; Whisper also accepts vocabulary prompt. |
| Snippets | R06; CRUD/search/import; user also requires export | CRUD/search/JSON+TSV import/export; literal expansion; shared storage | Core tests and UI save/restart verified. Plain text snippets; rich formatting is not inserted. |
| Commands | R09; selected/field rewrite and spoken instruction | Real local selected-text rewrite via configured shortcut; Scratchpad fallback | Model path works; selection/focus guards covered by owned insertion fixtures. Live external-app command speech not end-to-end verified. |
| Transforms | R10; presets, 8 custom prompts, samples, compare/retry/undo | Presets, eight custom prompts, examples, preview/apply/undo in Scratchpad | Real model generation and persistence; custom per-transform global hotkeys are not exposed in this release. |
| History | R01/R13/R24; search/copy/delete/audio/retry | Durable recovery before insertion; search/copy/delete/play/retry; retention | Core storage/transactions and real CLI audio. Successful audio retention off by default; failures recoverable. |
| Insertion | R03/R24; clipboard/focus/Unicode | Stop-time dictation field capture, late field selection, same-field return/caret refresh with unchanged text; original command selection; clipboard protection; Unicode; no submission by default | 46 owned native/WebView2 insertion and overlay checks passed in 0.1.1, including recreated browser fields and clipboard concurrency. Electron, elevated apps, physical x64 and IME remain device-validation gaps. |
| Notetaker capture | R15–16; mic+default output, explicit start/stop/resume, live visibility | Explicit mic + loopback tracks, resume, imports and interruption recovery | Real speech-file processing; previous mic test. Two-source live meeting and long live recording not end-to-end verified. Full ASR occurs after stop; long meeting processing is not streaming. |
| Meeting editor | R17–20; typed notes separate from summary/transcript, speaker labels, search/playback/export | Separate private notes/summary/transcript; source labels, optional two-model diarization; playback/search/export | Two synthetic voices split correctly on ARM64 and x64 emulation. Actual generated summary retains decision/deadlines/amount/negation. UI notes survive summary and immediate close. Overlapping speakers/echo not validated. |
| Meeting questions | R21; retrieval, citations, no unsupported answers | Local excerpt retrieval, bounded scope and source links; validated verbatim quotations | Real answerable and absent-information UI fixtures exercised. Invented-quote regression fixed with source validation. Excerpt retrieval may miss relevant wording; no guaranteed completeness. |
| Folders/shared library | R22; folders, sharing, permissions, sync | Folder CRUD; selected shared vocabulary/snippets/meeting folders; tombstones and deterministic merges | Two-store conflict/deletion/unshare/privacy tests pass. External storage permissions replace hosted accounts/invitations; provider concurrency not production tested. |
| Calendar | R19; Google/Outlook OAuth and reminders | Google/Outlook OAuth+PKCE, protected tokens, paginated read-only events, join links and reminders | Google/Outlook parsing and once-only reminder fixtures pass. Valid external app registrations/consent required; live connection not tested. Refresh is manual. |
| MCP | R23; read-only meetings search/detail/pagination | Opt-in read-only stdio protocol 2024-11-05, search/read/pagination; excludes private notes/audio | Actual child process protocol, privacy and mutation-rejection checks pass. Client-specific compatibility not fully evaluated. |
| Privacy/storage | R13; retention, delete/export, optional context | Local default, DPAPI optional keys, SQLite WAL transactions, export, retention/deletion | DPAPI encryption/roundtrip/deletion, atomic rollback, readable backup and sharing privacy checks pass. |
| Models/providers | User requirements; engine-research.md | Pinned hashes; staged bounded downloads; cancel/retry; offline native CPU; optional explicit HTTPS cloud writing | Recommended local files verified and exercised. Local HTTP fixtures passed range resume, corruption rejection preserving installed bytes, cancellation and partial retention. Exhaustive real-network failure scenarios remain untested. |
| Startup/resources | User requirements | Optional sign-in startup; lazy hub; model idle unload; hidden UI release after 30 seconds | Initial background 76.5 MiB working set/15.5 MiB private, 0.096% CPU over 20 s. Final hidden-interface release measured in testing report. |
| Packaging | Native ARM64/x64 plus automatic selector | Self-contained native ARM64/x64; auto architecture selector and standalone installers; per-user install | Final architecture inventories and installer lifecycle results are recorded in testing.md. Physical x64 remains unavailable. |
| Updates | Architecture-aware integrity and rollback | HTTPS RSA-PSS signed manifest, OS architecture selection, SHA-256/size, pre-update database backup | Tampering/wrong-architecture fixture checks pass. Hosting, signing credentials and real release delivery external; binary rollback after partial installer failure not implemented. |

## Intent and visual direction

User: everyday voice writing and notes. Actions: speak, recover, search, correct vocabulary, review meetings. Domain: voice, waveform, pause, transcript, paper notes, corrections. Reference color world: warm paper, white content, ink, sand selection, muted stone, amber New badge. Signature: quiet narrow sidebar around a large rounded white writing surface, compact black recording pill. Avoid generic metric-card dashboard, colored navigation rail, excessive decoration. Reference structure overrides conflicting generic skill preferences.

Fonts: acquire Figtree separately under OFL; use an independently licensed serif for editorial empty states. Geometry from observed reference; do not distribute original images. Main pages lead with a restrained 24px title, 800px content column, 36px controls, 4px spacing base, subtle borders. Settings use a centered two-column modal with warm left navigation. Every component uses these shared tokens and native accessible controls.

## Evidence gaps and intentional differences

- No claim to proprietary AI output, cloud internals or equivalent accuracy. Local models measured separately.
- Distinct name/icon/publisher, local account-free onboarding, local model settings replace subscription promotions.
- Original inserts into current focus; Murmur now chooses the dictation field at stop (or before insertion if no editor was chosen), allows returning to the same unchanged editor, and retains recovery on different-field or edited-text changes. Voice commands keep the original selection.
- Paid/account-gated capabilities stay in this checklist. Hosted identity, public releases, signing and OAuth registrations are external dependencies.
- Physical x64, multi-monitor, IME and microphone-disconnect tests require suitable available conditions; compile or emulation is not a substitute.

## 0.1.1 verification update

See [release-0.1.1.md](release-0.1.1.md) for measured Caps Lock activation, single-engine setup, email processing and UI changes. Historical 0.1.0 measurements remain in testing.md.

## 0.1.2 update

See [release-0.1.2.md](release-0.1.2.md) for individual/bulk downloads, headless layout checks, stop-time destination selection, long-dictation cleanup preparation, measurements and live-test limitations.

## 0.1.3 comparison and processing update

See [alternatives-comparison.md](alternatives-comparison.md) for the current free-alternative inventory and same-PC Handy benchmarks, and [release-0.1.3.md](release-0.1.3.md) for bounded exact-audio reuse and cleanup safeguards. Mac development is now authorized; source progress is tracked separately from tested Windows releases.

## Mac source preview

The first native Mac slice is in [macos/README.md](../macos/README.md): menu-bar setup, English SpeechAnalyzer capture, Caps Lock gesture, nonactivating animated orb and guarded AX insertion with disk recovery. This is source work only, not compiled or exercised on a Mac. No feature, performance or platform parity is inferred from its presence. The initial target is Apple silicon/macOS 26; nine core tests are supplied but await execution with Xcode.
