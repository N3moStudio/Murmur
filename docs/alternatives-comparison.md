# Murmur versus free Wispr Flow alternatives

Research checked November 4, 2026 (America/Los_Angeles). This is a broad survey of 14 relevant community alternatives, not an assertion that every small fork on the internet was found. Repository stars measure interest, not active users, reliability or accuracy. Features below are documented in current primary README/source, not independently verified in every shipped binary. All entries link to their owning projects. “Free source” and “free cloud usage” are different.

## Products and track records

| Project / creator | Stars at check | Latest GitHub release | Free use / platforms | Documented strengths compared with Murmur |
|---|---:|---|---|---|
| [Handy — cjpais and contributors](https://github.com/cjpais/Handy) | 37,567 | 0.10.0, Nov 2 | MIT, free local; Windows ARM64/x64, Mac, Linux | Streaming speech/preview, many languages/models, GPU backends, dictionary, post-processing, history, CLI, portable mode and isolated crash-recovering engine. Largest direct dictation alternative in this survey. **Downloaded and tested here.** |
| [Buzz — chidiwilliams and contributors](https://github.com/chidiwilliams/buzz) | 22,423 | 1.4.5, Aug 23 | MIT, free local; Windows, Mac, Linux | File/video/YouTube transcription, live captions, speech separation, speaker identification, subtitle exports, folder watching, plugins and CLI. Primarily a transcription/captions tool rather than a direct hotkey writing substitute. |
| [FluidVoice — Altic](https://github.com/altic-dev/FluidVoice) | 12,813 | 1.6.12, Oct 22 | Free GPL app; Mac today; Windows/iOS described as upcoming | Native live preview, several on-device models, selected-text rewriting, voice system commands, per-app prompts, notch-aware overlay, audio budgets. Its optional local Fluid Intelligence runtime is privately maintained; the whole stack is not GPL source. |
| [OpenWhispr — OpenWhispr contributors](https://github.com/OpenWhispr/openwhispr) | 10,747 | 1.11.0, Oct 29 | MIT, local core free; cloud/provider billing separate; Windows/Mac/Linux | Broadest documented suite here: dictation, translation, agents, live meeting diarization and voice fingerprints, calendar integration, semantic notes search, sharing/team roles, imports, MCP/API. Murmur does not yet match this breadth or production ecosystem. |
| [VoiceInk — Beingpax](https://github.com/Beingpax/VoiceInk) | 7,029 | 2.25, Nov 3 | GPL source builds free; distributed app has trial/paid license; Mac 15+ | Native app/URL modes, context awareness, personal dictionary, keyboard/mouse shortcuts, assistant and multiple local engines. Its “99% accuracy” is a vendor claim without a supplied evaluation corpus; not treated as a score. |
| [Whispering / Epicenter — EpicenterHQ](https://github.com/EpicenterHQ/epicenter/releases) | 4,838 (whole monorepo) | Whispering 7.11.0, Dec 27, 2025 | Free source; desktop/local and provider options | Published Whispering has shortcuts, transformations and local Whisper/Parakeet/Moonshine. **Current monorepo is being rebuilt around a different store and notes app**; current source readiness must not be inferred from old Whispering releases or star counts. |
| [OpenLess — Open-Less](https://github.com/Open-Less/openless) | 4,051 | 2.0.1-tauri, Oct 16 | AGPL; bring-your-own provider costs; Mac/Windows and mobile work | User-approved correction learning, optional cursor context, style packs, extensive ASR/polish provider support, local model options and Windows input-method integration. Some local Windows engines are explicitly experimental. |
| [FreeFlow — zachlatta / marcbodea](https://github.com/zachlatta/freeflow) | 3,043 | 1.3.1, Oct 9 | MIT/free app; default Groq key uses provider quota; Mac Intel/Apple Silicon | Context-aware cleanup, custom vocabulary, hold/toggle shortcuts, selected-text edits, optional original-field insertion and Paste Again recovery. Supports configuring local API servers; local engines are not a turnkey bundled default. |
| [Speech Note — mkiol](https://github.com/mkiol/dsnote) | 1,709 | 4.9.0, Jun 28 | MPL-2.0/free local; Linux/Sailfish | Offline speech recognition, read-aloud and machine translation with a large model/language catalog. Stronger language and accessibility breadth; not a Windows replacement. |
| [Amical — amicalhq](https://github.com/amicalhq/amical) | 1,598 | 1.12.5, Sep 25 | MIT/local-first; Mac/Windows | Active-app formatting, Whisper/Ollama, local-model setup, hotkeys/macros/widget. README labels smart voice notes in progress and meeting transcription/MCP planned; those are not counted as completed features. |
| [Voquill — voquill](https://github.com/voquill/voquill) | 1,024 | desktop 0.0.652, Sep 26 | AGPL source; local/BYOK choices, hosted-service terms separate; Windows/Mac/Linux | Glossary syncing, writing styles, local Whisper/GPU, audio history, voice assistant and backend choice. Murmur has comparable local writing controls but no equivalent deployed hosted/mobile ecosystem. |
| [Voicetypr — ideaplexa](https://github.com/ideaplexa/voicetypr) | 741 | 2.0.11, Nov 2 | AGPL source; packaged local use requires lifetime license after trial; Mac/Windows | CLI file transcription, remote transcription server, optional cloud, GPU sidecar/fallback and signed Mac builds. Included to distinguish free source from a free finished app. |
| [WhisPaste — whispaste](https://github.com/whispaste/whispaste) | 70 | 1.4.2, Oct 15 | MIT/free local; Windows/Mac/Linux | Offline Whisper/Parakeet, optional cleanup/translation, selection edits, snippets including forms, quick-paste/clipboard side panel and opt-in automation API. |
| [YazSes — MSKazemi](https://github.com/MSKazemi/yazses) | 36 | 2.48.1, Nov 3 | Apache-2.0/free local; Linux, Windows and Mac preview | Offline faster-whisper, documented voice-command grammar, file/meeting transcription and speaker labels. Less plug-and-play than the largest apps; platform readiness varies. |

Superwhisper and other paid products/free trials are not counted as fully free community alternatives. Built-in Windows/Apple dictation is also outside the “people's remakes” set. The survey used first-party repositories and release APIs; marketing roundups served only to discover names. Repository and release metadata are saved in `research/alternatives/` (local evidence, excluded from releases).

## Actual Handy test on this PC

Downloaded the official **Handy 0.10.0 ARM64 MSI**, verified its release SHA-256 (`a43a8675f1ca8b479a523943d78932df2b767a1ebf547e39d9f82eb27bf4adc0`), extracted a portable installation and executed its shipped binary. The portable marker directs settings/models into the research folder. No native window, microphone, clipboard or hotkey interaction was used. Source tag 0.10.0 confirms its headless path calls the same transcription manager, while skipping input devices/UI.

Hardware: Snapdragon X X126100 ARM64, 16 GB Windows PC. Both apps ran natively on CPU. Handy used its recommended **Parakeet Unified English 0.6B Q8**; Murmur used its normal **Moonshine English**. Their model sizes/architectures differ intentionally: this compares recommended product choices, not the same model implementation. No paid or cloud services handled audio.

Warm recognition times (median of three runs, milliseconds). Excludes model load, cleanup, key handling and insertion:

| Same audio | Handy 0.10.0 | Murmur 0.1.2 | Result |
|---|---:|---:|---|
| JFK public speech, 11.00 s | 628 | 212 | Murmur faster in this test |
| Numbers/negation synthetic, 14.09 s | 849 | 334 | Murmur faster in this test |
| Workshop email synthetic, 83.35 s | 5,850 | 2,314 | Murmur faster in this test |

All three raw transcripts had **0 format-normalized word errors** against their reference texts in both apps (22/33/256 reference words). Normalization ignores punctuation/case and explicitly equates the fixture's date, time, digit, currency and hyphen spellings. This tiny, mostly synthetic sample does **not** establish general 100% accuracy. Handy punctuated the JFK clip directly; Murmur's raw result had no punctuation until its separate writing pass.

Peak sampled aggregate process-family working set during the long ASR-only run: **Handy 1,465.5 MiB; Murmur 387.6 MiB**. Samples include Handy's worker, not just its small parent. These are active benchmark peaks, not idle RAM, and exclude Murmur's writing model. Aggregate working set can count shared pages across processes. Sampling interval 50 ms; run logs include process CPU time, but run-count/load differences make those unsuitable as a simple battery ranking.

Handy's optional Moonshine Streaming Small passed both short batch tests but its worker aborted twice on the 83-second batch clip (`GGML_ASSERT` in CPU ops, exit 0xc0000409). The app's automatic retry also failed. That is a reproducible failure of that particular model/path/build on this PC, **not** proof that Handy generally fails long dictation: its recommended Parakeet completed it.

### Streaming distinction

I also drove Handy's shipped worker protocol with the same files, in 200 ms PCM chunks, using its default streaming strategy and English hint. Parakeet's final flush took **443 / 412 / 466 ms** for the three clips; the long stream consumed about **29,220 ms** of cumulative feed-call compute before finalization. It retained the full text. This is file replay into the real native engine, not a live microphone/UI latency test. Streaming moves computation into speaking time, so batch time alone cannot rank end-to-end responsiveness.

Murmur 0.1.3 now also reuses exact previously decoded audio sections. The prepared 83-second fixture measured **347 ms recognition remaining + 1,955 ms writing = 2,306 ms final wait**, down from **4,342 ms** in 0.1.2 and **8,470 ms** before preparation in 0.1.1. Its raw transcript exactly matched the uncached run. This includes email cleanup; Handy's 466 ms flush does not. No claim of beating an unmeasured Handy cleanup setup is made.

## Honest verdict and resulting work

Murmur wins this small native ARM64 **batch speed and active ASR memory** comparison while matching the tested words. It is **not proven better overall**. Handy has a larger field-tested ecosystem, live text preview, more languages/GPU support and a much smaller installer. OpenWhispr documents a substantially broader collaboration/agent/meeting ecosystem. Mac-focused tools are ahead of Murmur on Mac availability and native integrations. No reliable cross-product universal accuracy or latency record was found.

Changes implemented from the investigation: exact-audio decode reuse (bounded text-only cache, no extra retained PCM), protection against added greetings/thanks and removed/added negation, safe per-section fallback caching, and the setup/focus fixes in 0.1.2. English remains the only supported language, with Spanish/French selected for later. Mac work follows the tested Windows update; it is not counted as a finished platform until compiled and exercised on a Mac.

Remaining comparisons that require real device time: first microphone packet and overlay latency, live focus switching in the user's apps, noisy/accented real speech, sleep/headset interruptions, battery, GPU-capable PCs and Mac. A millisecond table is an observation with measurement limits, not a promise that every utterance or computer wins by that amount.
