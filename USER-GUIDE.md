# Murmur guide

## Write with your voice

Click the text field where you want to write. Quickly tap Caps Lock twice, speak in English, then tap Caps Lock once. The bar shows your microphone level while recording and a Thinking Orb while processing, then disappears. Escape cancels. Alternatively hold Ctrl+Alt and release to finish. Change these shortcuts in Settings. Caps Lock is reserved for dictation while Murmur runs; Shift+Caps Lock toggles capitalization. Standard Ctrl+C and Ctrl+V do not activate recording. Shortcuts owned by another app may conflict even when Windows accepts them.

The first dictation after inactivity loads the local models. Subsequent speech is faster. Murmur prepares cleanup during pauses and periodically during longer continuous speech. Finished sections are reused only when their final recognized text and style match; the full recording is always checked. Long cleanup is divided into bounded sections so the end is not silently dropped by a generation limit. Speech models unload after 30 seconds of inactivity and the writing model after 45 seconds. Idle animation is stopped. Settings → System → Thinking orb offers Animated, Follow Windows, and Still. Animated keeps the working orb moving even when Windows animation is disabled, at 15 fps on battery or 30 fps on power. Microphones are closed while idle.

Murmur does not press Enter after insertion unless you enable that setting. Ordinary dictation chooses the focused field when you stop recording, so you can click into it while speaking. If no editor is selected then, you can select one before insertion. You can leave and return to the same unchanged field during processing, including moving the cursor. Editing its text or switching to a different field after stop keeps the result in History. Choose Copy, or Retry insertion and focus the intended field during the countdown. Selected-text commands keep their original selection. Some elevated apps and controls that do not expose an editable field require manual paste.

## Dictation setup and writing style

Use the download arrow beside a component, or Download all beneath the three components, during setup or in Settings → Dictation setup. Downloads run one at a time and skip installed components. You can pause and resume them. One English speech engine, a speech detector, and a separate writing component are managed together. Their combined download is about 1.26 GB. Model files are verified before use. Downloads can be cancelled and retried. Raw dictation works without the writing model; a cleanup failure keeps the transcript with an explanation.

English is the only supported language. One speech engine handles every app; writing cleanup adapts to email and chat context. This release uses CPU inference.

Style controls punctuation and formatting. Email, work chat, personal chat and document context are detected from the current application, window title and focused field when context detection is enabled. This is a local heuristic and can misclassify unfamiliar apps. Per-application overrides are available. Detection does not read your inbox. Always review names, amounts, dates and important text before sending it.

Dictionary entries map a spoken form to your preferred spelling. Snippets expand a phrase into literal text such as a signature. Both support editing, deletion, search, JSON/tab-separated import, and export. Snippets preserve literal dollar signs and Unicode. A shared entry requires a configured shared folder.

## Commands and transforms

Select text in another application, hold Ctrl+Alt+Shift, say an editing instruction, then release. If the application does not expose the selection, use Scratchpad. Transforms provide presets and up to eight custom prompts with writing examples. Scratchpad supports preview, applying and undoing a transform. Generated edits can change meaning; review them.

## Meetings and voice notes

Open Notetaker and choose Start Notetaker, or import an audio recording. Recording starts only when you explicitly choose it. The recording bar stays visible throughout capture. System audio and microphone are separate sources. Stop to produce a transcript and summary; longer meetings need longer to process. Resume adds another session to the same note.

My notes contains your typed personal notes. Summary is generated separately and can be edited or regenerated. Generated summaries are drafts grounded in the transcript, not guaranteed factual records. The transcript has timestamps, search, speaker renaming and playback. Export produces Markdown including your personal notes; check its contents before sharing.

Default speaker labels identify microphone and system audio. Use Set up speaker identification in Notetaker settings, then enable it (about 41 MB) to distinguish speakers within a source. Mixed turns are marked when uncertain. The tool does not identify real-world identities automatically. Playback and source separation can contain speaker overlap or microphone echo.

Questions over meetings return verified, verbatim transcript excerpts with source links. The local model selects relevant excerpts; Murmur rejects any quote that does not exist in its stated source. Personal notes are excluded. Retrieval is bounded and can miss relevant wording; an absent answer is not proof that a fact never occurred.

## Folders and shared libraries

Choose a folder on shared storage, a network share, or your file-sync provider, then use Sync. Share selected vocabulary, snippets and meeting folders. Typed personal notes and audio are excluded. Access permissions come from that storage service or Windows; Murmur has no hosted accounts or invitation system. The newest public edit wins conflicts; simultaneous edits on separate devices may need another sync. Keep backups before large shared changes.

## Calendar setup

Google and Outlook integrations require your own desktop OAuth registration. Murmur requests read-only calendar access and stores tokens with Windows user encryption. It cannot connect without a valid client ID and the provider's consent. No provider registration is bundled with this unsigned local release.

For Google, create an OAuth Desktop app, enable Google Calendar API, configure consent/test users, and enter the client ID and any client secret required by that registration. For Microsoft, register a public desktop client supporting the accounts you intend to use, enable loopback localhost redirection and delegated Calendars.Read/offline_access. The app uses a random local loopback port and PKCE. Never enter credentials into an untrusted provider page.

After connection, Refresh calendar in Murmur. Upcoming events and reminders use the last refreshed calendar. Join opens the event's HTTPS link. Start recording is a separate explicit action; joining does not start a recording.

## Local MCP access

Enable local MCP in Settings, then configure your MCP client to launch the installed `Murmur.exe` with argument `--mcp`. Use the displayed executable path. This read-only stdio server supports protocol 2024-11-05, search_meetings and paginated read_meeting. It excludes dictation, personal notes, audio and credentials. Any local client you configure to launch it can read saved meeting transcripts and summaries. Disable it to refuse new requests.

## Privacy, backup and recovery

Local processing is the default. Optional cloud writing requires an explicitly enabled HTTPS provider, your own API key and model name. That provider receives the text being processed and may charge your account; review its pricing and privacy terms before enabling. Speech recognition remains local. There is no silent paid fallback.

History retention and optional successful-dictation audio retention are configurable. Failed or cancelled processing remains recoverable until you delete it. Meetings retain recordings for playback. Use History and Notetaker deletion controls to remove entries and owned recordings. Export data saves a SQLite backup; keep associated recordings separately if you need a full audio backup. Close Murmur before replacing its database with your backup. The dictation components can be downloaded again from Dictation setup.

Closing the hub keeps the tray and shortcuts running. Quit from the tray stops the app. Startup at sign-in is optional. Settings → Help opens this guide. Upgrades preserve your data. An update feed is not configured for this local release; install a new supplied setup package when available. Public automatic updates require release hosting and a publisher verification key.

## Troubleshooting

If microphone data does not arrive, check Windows microphone access for desktop apps and select the correct device. A disconnected device stops capture and preserves what was recorded. No automatic recording restarts after sleep. If a model fails integrity verification, use Repair dictation setup to verify and repair its components. If an app blocks insertion, copy from History. Keep Murmur and the destination at the same Windows privilege level; manual paste is the supported elevated-app fallback.
