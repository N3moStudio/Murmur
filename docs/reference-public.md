# Public reference inventory — Wispr Flow for Windows

Research date: **2026-11-04 (America/Los_Angeles)**. Parent investigation identified installed **1.6.1376**; this document did not inspect that package. Evidence below is current official web documentation, not observed operation of that installed version. Several help articles changed within hours of research and can describe newer server-gated behavior. No proprietary cloud internals are established here.

Every item below is a requirement candidate, including paid features. Implementation status is **not assessed by this research pass**. A feature is not complete merely because a control or package string exists. The lead checklist must record implementation and verification separately.

## Sources, behavior and verification requirements

### R01 — Desktop structure and settings

Source: [Desktop navigation](https://docs.wisprflow.ai/articles/5096240724-navigating-the-wispr-flow-app-desktop-ios-and-android).

Documented for Windows/Mac: Hub with Home and a gated meeting area; Settings and help at sidebar bottom. Home has date-grouped searchable history, hover copy/report actions, word statistics and shortcut information. General settings cover shortcuts, microphone, languages and appearance; System covers login launch, overlay and notifications; Account covers identity; a separate billing page and privacy/data page exist. Light/dark are selectable; automatic theme is listed but disabled in current documentation. Windows tray menu opens the Hub and help.

Test: inspect all routes with mouse and keyboard, switch themes, reopen from tray, search/copy real history, check disabled controls explain themselves. **Visual gap:** exact Windows dimensions, sidebar ordering, fonts and route visibility need package/runtime screenshots. Current docs sometimes say Meetings and sometimes Notetaker.

### R02 — Global shortcuts

Source: [Shortcut configuration](https://docs.wisprflow.ai/articles/2612050838-supported-unsupported-keyboard-hotkey-shortcuts).

Windows bindings persist per device across updates, without cross-device sync. The recorder accepts up to three keys, supported modifier-only combinations, middle mouse and buttons 4/5. Most actions accept four bindings; cancellation accepts one. Reserved Windows shortcuts and duplicates are rejected; opposing sides of the same modifier cannot share a binding. Editing takes effect immediately. Cancellation leaves old binding intact. Windows Escape also reaches the foreground app. A conflicting hands-free subset produces a warning. Some actions cannot lose their final binding. Reset restores defaults.

Test: press/release, modifier order, held-key recovery, duplicates, reserved keys, mouse buttons, restart persistence and rapid presses. Reproduce documented behavior only where appropriate; clearly distinguish our safer conflict prevention.

### R03 — Dictation lifecycle and defaults

Sources: [First dictation](https://docs.wisprflow.ai/articles/6409258247-starting-your-first-dictation), [Hands-free](https://docs.wisprflow.ai/articles/6391241694-use-flow-hands-free).

Windows defaults: push-to-talk **Ctrl+Win**; hands-free **Ctrl+Win+Space**. Hold/release records and inserts into the field focused at insertion time. A double press within 500 ms locks hands-free; a third quick press cancels. A very short push-to-talk tap cancels after waiting for a second press. Clicking the overlay starts/stops hands-free without changing the intended field. Cancellation supports Escape and an overlay action. Recording warns near 19 minutes and ends at 20. Silence produces no insertion. Recovery can retry stored audio.

Optional spoken Enter behavior is disabled by default. When enabled, a trailing instruction may submit after successful insertion; mid-sentence and command-mode occurrences remain text. Our implementation must preserve the user's explicit no-submit default.

Test: real microphone, silent audio, all transitions, key bounce, cancellation, long-session limit, focus changes, no accidental submission. **Intentional safer deviation candidate:** retain original target and require retry if focus changed rather than paste into a newly focused app.

### R04 — Onboarding

Source: [Initial setup](https://docs.wisprflow.ai/articles/3152211871-setup-guide).

Documented desktop flow includes introduction/setup questions, privacy choices, microphone permission and a live input meter, microphone switching, shortcut selection, language selection, practice dictations and entry into the Hub. Windows uses its microphone privacy settings; the permission step may be skipped when granted. Practice can be skipped. Microphone error states distinguish absent and occupied devices.

Test: fresh isolated user data, denied/unavailable microphone, changing devices, complete/skip practice, restart during setup, onboarding completion persistence. **Unknown:** exact Windows screen sequence and all branching questions in 1.6.1376. Use distinct local product onboarding without fake account creation.

### R05 — Dictionary

Source: [Dictionary](https://docs.wisprflow.ai/articles/4052411709-teach-flow-your-words-with-the-dictionary).

Windows sidebar opens a word library with add/edit/delete, personal/shared entries and file import. Fields cap at 60 characters; whitespace and symbol-only entries are rejected. Import accepts up to 3 MB/1,000 entries. An entry can map a recurrent misspelling to an exact desired spelling; whole-word matching ignores case, preserves saved replacement spelling, prefers longer overlaps, and personal entries take priority. Optional correction learning exists but deliberately excludes paths, URLs, code identifiers, numbers and common vocabulary. Organization disablement hides management without deleting saved entries.

Test: names, technical words, whole-word boundaries, overlapping phrases, Unicode, duplicates, import validation and exact replacement. Automatic learning requires actual correction observation, not indiscriminate vocabulary harvesting. Import/export file formats are not established by this source.

### R06 — Snippets

Source: [Snippets](https://docs.wisprflow.ai/articles/5784437944-create-and-use-snippets).

Windows supports personal/shared libraries, creation/editing/deletion, search/selection and file import. Trigger limit is 60 characters; expansion limit is 4,000 including formatting. Import accepts 3 MB/1,000 items. Trigger phrases expand alone or within sentences with word-boundary matching; longer overlaps win and personal entries take priority. Stored casing is preserved. Rich text supports emphasis, lists and links, falling back to plain text where needed. Dynamic variables are not supported. Keyboard saving and multirow deletion are documented.

Test: punctuation, repeated triggers, multilingual boundaries, longest match, rich/plain paste, literal angle brackets, CRUD restart persistence and invalid imports. **Unknown:** reference export availability and exact import schema; user explicitly requires both import/export regardless.

### R07 — Style by application

Source: [Styles](https://docs.wisprflow.ai/articles/2368263928-how-to-setup-flow-styles).

Desktop categories are personal messaging, work messaging, email and other; all default to formal. Choices include formal, casual, very casual for personal, and excited for work/email/other. A guided category setup precedes category cards. Selections save immediately. Formal retains capitalization/punctuation; casual reduces punctuation; very casual reduces capitalization; excited adds emphasis. Messaging final-period behavior depends on style. Styles are documented as English-only. Terminals and AI assistants usually belong to other.

Test: each category, restart persistence, changed target app, names/URLs/code/numbers preservation. **Unknown:** exact application classification database and proprietary rewriting behavior. Our categories must be transparent/configurable rather than pretend exact identification.

### R08 — Cleanup strength

Source: [Auto Cleanup](https://docs.wisprflow.ai/articles/4283510616-auto-cleanup-control-how-much-flow-edits-your-dictation-beta).

The desktop Style page offers raw, light and medium cleanup through selectable cards. Light is default when unset. Raw retains fillers/false starts; light removes fillers and corrects grammar; medium can rephrase for clarity. A former high level is now represented as medium. New selections apply to subsequent dictation without restart.

Test: separately benchmark raw recognition and cleanup against labelled speech; verify preserved numbers, names and intent. No local engine should be described as identical to Wispr's proprietary result.

### R09 — Command Mode (paid desktop feature)

Source: [Command Mode](https://docs.wisprflow.ai/articles/4816967992-how-to-use-command-mode).

Windows default is **Ctrl+Win+Alt**, with activation disabled by default in experimental settings. Voice instructions can edit selected/current field text or draft a reply; selection fallback uses copy. Release executes; double press can lock hands-free. Commands receive field content and selection. Returned edits bypass ordinary dictation formatting. Web-search commands open a search service. Empty/noneditable target and server failure states are documented. Access is plan-dependent but remains an implementation requirement.

Test: explicit selection capture, clipboard restoration, source text unchanged on failure/cancel, stale-target rejection and genuine local instruction execution. **Dependency:** a capable local language model or explicit optional provider. Do not replace arbitrary instructions with fabricated success or a few undocumented keyword substitutions.

### R10 — Transforms

Source: [Transforms and custom prompts](https://docs.wisprflow.ai/articles/8068950331-how-to-use-transforms-beta).

Desktop adds a Transforms route with polish and prompt-structuring presets, plus eight custom instruction slots. Windows defaults are Win+Alt+1 and Win+Alt+2; change comparison uses Win+Alt+O. Selection/field input range is 1–3,000 words. Rewrite can auto-paste or open in Scratchpad, supports comparison, retry and undo of the latest result. Each custom transform needs name, prompt and shortcut; edits autosave. Polish has independent built-in rules and custom rules. Up to five writing samples, 50–500 words each, can be attached to a prompt. Cancellation and concurrent-request prevention are specified.

Test: valid/oversize/no selection, undo, retry from original, custom persistence, failed provider, focus/clipboard changes and private sample handling. **Gap:** exact comparison UI, sample editor and all autosave error states need observation.

### R11 — Microphone selection

Source: [Microphones](https://docs.wisprflow.ai/articles/8884408990-connect-and-set-up-external-audio-devices).

Desktop input picker is reachable from overlay context menu and General settings. Automatic selection and explicit device selection exist; virtual inputs require deliberate expansion/selection. A new physical device can prompt to switch. Changes take effect next dictation. Missing devices expose refresh/system sound settings, and disconnect during dictation can expose insertion recovery. Microphone ranking is documented for **Mac only**; do not silently treat that as Windows evidence.

Test: default/explicit device, hotplug, disconnect while recording, exclusive use, access denied and live meter. Never keep microphone open at idle just to make the meter decorative.

### R12 — Floating overlay

Source: [Overlay movement/docking](https://docs.wisprflow.ai/articles/1790396454-move-and-dock-the-flow-bar-on-desktop).

Desktop overlay shows idle/recording/processing, can drag to bottom/left/right edges and remembers the edge. Default is bottom. It follows the pointer's display, avoids the Windows taskbar and leaves room for auto-hide. Settings can hide idle indefinitely; its idle context menu can hide for an hour. Active meetings make it visible. Setup restricts actions and dragging. Optional meeting screen-capture exclusion applies to recorder/overlay, not all app windows.

Test: focus stays in target, monitor coordinates, taskbar edges, DPI changes, drag cancellation, hide expiration and active-session visibility. **Visual gap:** waveform geometry, colors, exact size, animations and notification placement require screenshots at known scale.

### R13 — History/privacy

Source: [Data preferences](https://docs.wisprflow.ai/articles/9609615338-private-cloud-sync-and-data-sharing-preferences-in-wispr-flow).

Reference separates model-improvement consent, dictation cloud storage and local history; one setting does not imply another. Organization rules can lock controls with an explanation. Current documentation says meeting processing/storage remains cloud-based, while other newer articles describe locally retained meetings waiting for sync; these are distinct from our required offline processing. Do not copy reference defaults that conflict with the user's local-first request.

Test: explicit retention choices, export/delete, credential protection, no routine transcript logging, offline usage and no unsolicited network audio upload. **Uncertainty:** reconcile current server storage wording and exact local-history retention with package/runtime evidence.

### R14 — Windows support/reference limits

Source: [System requirements](https://docs.wisprflow.ai/articles/1036674442-Supported-devices-and-system-requirements).

Official reference Windows dictation support is Windows 10/11 **x64**, with 8 GB RAM recommended and about 200 MB space. Notetaker requires 16 GB RAM/2 GB storage. This does not establish native ARM64 support. The user's native ARM64 requirement is an intentional extension and needs independent binary and runtime verification.

Test: inspect each shipped executable/library architecture, real native ARM64 audio/inference, x64 compilation plus separate runtime verification; document physical-device availability accurately.

### R15 — Notetaker setup and source capture

Source: [Notetaker setup](https://docs.wisprflow.ai/articles/3665250541-getting-started-with-notetaker-beta).

Current docs explicitly support Windows. Calendar is optional. Sidebar, homepage widget and Ctrl+N can start an unscheduled note. Windows has no separate system-audio setup permission; it captures **the default output device**, with no independent output selector. Selecting another output only in the meeting app can omit participants. Closing the note does not end capture. Typed thoughts remain separate from summary. Audio failure and short/silent meeting states are explicit.

Test: microphone plus loopback on labelled test audio, visibly initiated recording, no capture before permission/setup, note reopening without lost state, device failure/recovery. **Unknown:** precise Windows onboarding/permission visuals and account-gated recorder screens.

### R16 — Notetaker recording lifecycle

Source: [Meeting recording](https://docs.wisprflow.ai/articles/9238501024-recording-a-meeting-with-notetaker-beta).

One meeting records at once. Start/stop/resume, floating note and overlay controls are supported. The note can open hidden, normally or beside a meeting window. Automatic call detection/start is optional and off by default; eligible countdowns can be cancelled. Recording length choices are 30 minutes/1/2/3 hours, default 2 hours. Stop saves audio before further starts/resumes; interrupted recording/upload can recover on launch. Offline audio may wait for processing. Windows loopback recovery preserves timing with silence; unrelated Mac sleep rules must not be attributed to Windows.

Test: repeated actions, stop during speech, cancellation, visible ongoing capture after window close, interrupted saving, low disk, resume and gap timing. Automatic detection is a separate requirement, not permission to begin recording without explicit opt-in.

### R17 — Notes/editor/export

Source: [Meeting editor](https://docs.wisprflow.ai/articles/9406970664-meeting-notes-and-the-editor-in-notetaker-beta).

Meeting view separates private typed thoughts, generated summary and transcript; attached documents add another tab. Titles/notes autosave. Summary can be edited after generation; regeneration must not overwrite private notes and needs deliberate replacement of edited summaries. Floating and Hub views coordinate a single editor. Summary/transcript search uses Ctrl+F and match navigation. Full Markdown copy includes private thoughts, unlike sharing; source-specific exports need clear scope. Shared notes are read-only and omit private thoughts.

Test: independent persistence, autosave interruption, summary regeneration, search and Unicode export, windows coordination and read-only behavior. **Gap:** rich editor formatting detail and attached-document workflows need direct observation.

### R18 — Summary generation

Source: [Meeting summaries](https://docs.wisprflow.ai/articles/1422535682-flow-summaries-in-notetaker-beta).

Finished recordings produce topical recaps, decisions and next steps with owners when evidence supports them. Short meetings may require manual generation; silent/insufficient audio ends with an explanation. Summary language follows spoken language, not typed notes. Names and numerical details should be preserved. Reopened/resumed recordings can update an unedited summary; edited summaries are preserved until explicit regeneration. Failure retains prior summary and audio where recoverable. Summary-only copy and full meeting Markdown have distinct contents.

Test: known meeting fixture with decisions/actions, absent owners, silence, short meeting, cancellation, resume, retained edits and provenance. Real local language-model processing is required; template filler and invented decisions do not satisfy it.

### R19 — Hub/calendar/search

Sources: [Meeting hub](https://docs.wisprflow.ai/articles/9247277218-the-notetaker-hub-your-meetings-in-one-place-beta), [Calendar reminders](https://docs.wisprflow.ai/articles/8955305188-meeting-detection-reminders-and-calendar-in-notetaker-beta).

Hub offers upcoming meetings, past date-grouped notes, search, meeting previews, join/record actions, and per-note actions. Upcoming covers seven days/up to 50 events, initially three rows, with conflict indication. Google and Outlook calendar connectors use browser authorization and show the connected email. Manual refresh exists. Disconnect removes upcoming/reminders but preserves notes. Joining and recording are distinguishable actions; viewing a preview alone does not record.

Test: empty/unconnected/offline views, local notes independent of calendar, event time zones, overlaps, joining without recording and disconnect preservation. **External dependency:** real OAuth application registration and account consent; an ICS import is useful but not equivalent to a functioning Google/Outlook connector.

### R20 — Live transcript/speakers/playback

Source: [Live meeting transcript](https://docs.wisprflow.ai/articles/4091106439-the-live-transcript-in-notetaker-beta).

Live text may be provisional and follows speech until the user scrolls/selects/searches. Labels can distinguish microphone from system audio or numbered speakers. Automatic participant names use supported visible meeting windows; unknown/ambiguous names remain numbered. Windows support varies by meeting app, with readable English Zoom windows required. Speaker clips support identification and renaming. Final transcript/speaker corrections become available after recording ends. Search, whole/partial copy and recording gap markers are documented. Live translation is offered for supported languages.

Test: two labelled sources, timestamps, select/scroll retention, manual rename, playback, silence gaps, absent identity evidence and translation boundaries. **Important gap:** microphone-versus-system separation alone is not multiple-speaker diarization; do not claim full speaker parity from two audio tracks.

### R21 — Questions over meetings

Source: [Ask about meetings](https://docs.wisprflow.ai/articles/5694642921-ask-wispr-chat-with-your-meetings-and-notes-beta).

Meeting-specific questions use that meeting; Hub questions search broader history. Answers cite records actually read, disclose missing evidence and offer source links. Chat can explicitly edit notes or an existing summary; an ordinary question must not rewrite them. Search scope respects sharing and dates/time zones. Sending another request can cancel the previous one; history, stop, copy and delete actions exist. Shared-note chat cannot edit source content.

Test: answerable/unanswerable queries, source citations, date boundaries, deleted data, cancellation and no implicit note mutation. **Implementation requirement:** retrieval plus real language-model response; a title keyword list is not equivalent to grounded meeting answers.

### R22 — Folders and shared library

Source: [Shared folders](https://docs.wisprflow.ai/articles/5861499910-share-a-notetaker-folder-with-other-people).

Windows/Mac support personal/shared folders. Owner controls folder identity and sharing; contributors can file their own notes. Name limit is 200 characters and description 2,000. Notes can belong to folders; deleting a folder preserves notes but removes access granted by the folder. Sharing can target individuals and eligible groups. Removing/leave actions have role-specific restrictions. Folder view shows newest 500 notes. Access requires account/organization cloud capability.

Test: local folder CRUD and memberships, delete without deleting notes, shared permissions, rename/sync conflicts and revoked access. **Evidence conflict resolved by recency:** older official marketing articles say there are no shared folders; this current help article explicitly documents them. Do not omit folders based on older marketing copy. **External dependency:** shared identity, storage and access enforcement.

### R23 — MCP

Source: [MCP connection](https://docs.wisprflow.ai/articles/9551372685-connect-an-mcp-client-to-wispr-flow-remote-mcp-server).

Settings includes integrations for Claude/ChatGPT/Gemini/Cursor and a remote server URL. Browser authorization is account-wide. Protocol access is read-only: meeting content, calendar metadata, participants and accessible shared notes; it excludes dictation and meeting-chat history and cannot modify/share/delete content. Search and detail retrieval have different scopes, with long transcripts paginated. Existing authorized clients remain subject to current access rules.

Test: initialize/list tools/search/read/pagination, explicit activation, no unauthenticated network exposure, no mutation tools and no dictation leakage. **Local equivalent:** a user-enabled stdio or authenticated loopback MCP server is reasonable; it is not hosted cross-device access. Public hosting/OAuth needs infrastructure.

### R24 — Insertion recovery and clipboard

Sources: [Terminals](https://docs.wisprflow.ai/articles/6478598909-using-flow-with-linux-wsl-and-terminal-applications), [Outlook](https://docs.wisprflow.ai/articles/9272331366-use-flow-with-microsoft-outlook).

Windows recovery defaults: paste last **Alt+Shift+Z**, copy last **Alt+Shift+X**. Reference attempts paste into current focus and may fail in noneditable terminals without warning. Outlook documentation says dictated text remains in the clipboard for about three seconds before restoration; a focus change can redirect insertion. WSL/VM/remote behavior is specifically limited despite some terminal support. Our implementation must handle concurrent clipboard updates rather than blindly restore old contents.

Test: browser plain/rich fields, native edit control, Electron editor, Unicode/multiline, changed/closed target, clipboard conflict, elevated target refusal and explicit copy/retry. Keep no-auto-submit default.

### R25 — Marketing reference and language range

Source: [Notetaker product page](https://wisprflow.ai/notetaker).

Public demonstrations show recorder tabs, active duration/stop, catch-up questions, generated summaries and resume. Marketing lists 21 supported meeting languages, distinct from 100+ dictation languages. It advertises imports from Granola/Otter and MCP context sharing. These demonstrations establish advertised screens/workflows, not pixel-equivalent Windows rendering, current account access, runtime accuracy or exact import behavior.

Test: distinguish dictation/meeting language support in UI; inventory imported transcripts/summary/source metadata rather than fake connector buttons. Provider imports require verified supported export/API formats and credentials where appropriate.

## Visual assets and observation gaps

The text extraction of researched help articles supplied no screenshot assets usable for measured Windows comparisons. Official marketing asset links found on the Notetaker page:

- Catch-up demonstration: `https://cdn.prod.website-files.com/682f84b3838c89f8ff7667db/6a70f1a6701b0ed33a4602fc_What%20did%20i%20miss%204.webp`
- Summary demonstration: `https://cdn.prod.website-files.com/682f84b3838c89f8ff7667db/6a70f1a6a5ffa536343100a3_Summary%206.webp`
- Import illustration: `https://cdn.prod.website-files.com/682f84b3838c89f8ff7667db/6a4ea68316766f400785ed11_switch-graphic.svg`

The web tool could not open these image links; **their visual content has not been inspected by this research pass**. Keep them as source pointers, not proof of exact component geometry. Do not redistribute Wispr logos/illustrations as our assets.

Required direct observations remain: Windows onboarding; Home at equivalent viewport; dictionary/snippet list and dialogs; Style cards; Transforms setup/editor/comparison; settings subpages; idle/recording/processing/failure overlay; microphone test; tray menu; Notetaker list/recorder/summary/transcript/folders/chat; light/dark; focus/hover/disabled/error states; keyboard traversal; scaling and multiple monitors. Capture each with application version, OS, window dimensions and display scale. Paid/server-gated surfaces remain evidence gaps if inaccessible, while their functional requirements remain in scope.

## Remaining research questions

- Exact 1.6.1376 Windows route availability versus current cloud-gated documentation.
- Full Scratchpad workflow, notifications preferences, current usage page, personal/team library navigation, transforms comparison visuals.
- Notetaker settings details, retention durations, note sharing roles and export formats, calendar OAuth scope, connector/import supported formats.
- Exact multilingual translation and speaker-diarization quality; public marketing cannot establish measured parity.
- Windows startup/minimize/close-to-tray and overlay no-activation implementation behavior.
- Package/resource extraction may establish assets and style tokens, but must not be mistaken for observed UI or licensed redistributable assets.

The current inventory is enough to prevent omission of major documented areas before substantial implementation. It is **not** a claim of complete UI parity, exhaustive public-document review or runtime validation.
