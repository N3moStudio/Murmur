# Murmur

Free, open-source English dictation for Windows 11, by **N3mo Studio**. Speak into the apps you already use, with local speech recognition and writing cleanup. No account or subscription is required for the local features.

## Download for Windows

### [Download Murmur Setup](https://github.com/N3moStudio/Murmur/releases/latest/download/Murmur-Setup.exe)

1. Run **Murmur-Setup.exe**. It detects Windows ARM64 or Intel/AMD x64, downloads the matching installer, checks its SHA-256, and opens setup.
2. Open Murmur and choose **Download all** for the three English components (about 1.26 GB total). Individual download buttons are also available.
3. Double-tap **Caps Lock** to start speaking. Tap it once to finish. Your text goes into the selected editor; the small recording orb disappears when finished.

An internet connection is needed to download setup, missing Microsoft runtimes, and the initial models. After that, the local features work offline. The current release is **unsigned**, so Windows may show an unknown-publisher warning. Download only from this project's releases; checksums are supplied.

[All downloads and release notes](https://github.com/N3moStudio/Murmur/releases/latest) include standalone ARM64/x64 installers and a larger combined installer for keeping both Windows builds together. The combined installer also detects the correct architecture. Model files are downloaded separately in every version.

## What it does

- English dictation, punctuation and local cleanup, with app-aware email/chat/document writing styles.
- Double Caps Lock hands-free recording, single-tap stop, Escape cancellation, and Ctrl+Alt push-to-talk.
- A small nonactivating recording/processing overlay using the MIT-licensed Thinking Orbs animation. It stays hidden while idle.
- Personal vocabulary, snippets, selected-text commands, history and recovery for unsuccessful insertions.
- Voice/meeting notes, optional system-audio recording, summaries, action items, search and export.
- Local settings and storage, optional explicitly configured providers and integrations.

Implementation and actual testing are recorded separately in the [feature checklist](docs/parity.md). Calendar connections need provider registrations; shared folders are not a hosted team-account service. Physical x64, some app/input-method combinations and other device cases remain unverified. See the [user guide](USER-GUIDE.md) and [known limits](docs/testing.md).

## Platforms

| Platform | Status |
|---|---|
| Windows 11 ARM64 | Native build; tested on Snapdragon X |
| Windows 11 Intel/AMD x64 | Native build; physical x64 device validation pending |
| Apple silicon Mac | [Initial source preview](macos/README.md), targeting macOS 26+; no Mac installer yet |

Windows needs 8 GB RAM minimum (16 GB recommended). The ARM64 build requires Snapdragon X / ARMv8.7 or newer. No NVIDIA GPU, NPU or paid inference service is required. Models unload after inactivity; the microphone closes while idle.

## Privacy and installation

Application files: `%LOCALAPPDATA%\Programs\Murmur`. Personal data and models: `%LOCALAPPDATA%\N3moStudio\Murmur`. Closing the hub leaves the tray app running; choose **Quit** to exit. Startup at sign-in is optional. Uninstall keeps personal data/models unless you explicitly select their removal.

Local processing is the default. Optional cloud writing or calendar access must be configured explicitly and is subject to the selected provider's terms. Murmur does not automatically submit messages after dictation.

## Source, builds and measured results

- [Build and release instructions](docs/build-release.md)
- [Contributing](CONTRIBUTING.md)
- [0.1.3 verification and improvements](docs/release-0.1.3.md)
- [Fourteen free alternatives and measured Handy comparison](docs/alternatives-comparison.md)
- [Mac source and validation status](macos/README.md)
- [Third-party licenses](THIRD-PARTY-NOTICES.md)

The Windows app uses .NET, WASAPI, UI Automation and WebView2. Moonshine handles English speech, Silero gates silence, and a native llama.cpp process runs Qwen for writing. The public repository includes application code, tests, installer/build scripts and licensed source adaptations. Test recordings, local user profiles, private development notes and downloaded research packages are excluded.

Murmur is independently implemented and is not affiliated with Wispr. Its local models do not promise identical output or universal performance superiority. Original Murmur code is [MIT licensed](LICENSE); dependencies and models keep their respective licenses. You may use, modify and redistribute it under those terms.
