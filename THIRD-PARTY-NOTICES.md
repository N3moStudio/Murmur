# Third-party notices

Murmur's original code is MIT licensed; see LICENSE. Full bundled notices are in licenses/ and corresponding third_party/ directories. No Wispr source, assets, fonts extracted from Wispr, or proprietary cloud implementation is included.

| Component | Version / use | License |
|---|---|---|
| .NET / Windows Desktop runtime, Microsoft.Data.Sqlite | 10.0.14; SDK 10.0.403 | MIT and included runtime third-party notices |
| Microsoft WebView2 SDK | 1.0.4314.35 | Microsoft SDK license and NOTICE; installed Evergreen runtime has Microsoft terms |
| NAudio | 2.2.1, audio capture/playback | MIT, Mark Heath and contributors |
| Whisper.net / whisper.cpp | 1.9.1; Whisper fallback and Silero VAD | MIT |
| sherpa-onnx | 2.0.1 native runtime and checked C# config interop | Apache-2.0, k2-fsa contributors; source adaptations documented in third_party/sherpa-onnx/PROVENANCE.md |
| ONNX Runtime | bundled by sherpa-onnx | MIT; upstream third-party notices included |
| llama.cpp | b12110 native CPU server | MIT; LLVM OpenMP license included |
| SQLitePCL.raw | 2.1.13 | Apache-2.0, Copyright 2014–2024 SourceGear, LLC |
| SQLite | bundled e_sqlite3 | Public domain |
| Thinking Orbs | 0.3.2, commit 54722cc9a3dcb167037f7d449d6cca4d174e9eb5 | MIT, Jakub Antalik; native geometry port and web scheduler modifications |
| OpenWhispr context classification | 1.11.0, commit d5821a7c88f4c1803b06c51b4580f16c51dd0dbe | MIT; attribution in third_party/openwhispr |
| Figtree | independently obtained font | SIL Open Font License 1.1 |

Models are downloaded separately, not embedded in setup. Exact revisions, hashes, sizes and license labels are pinned in src/Murmur/models.json. Moonshine English weights use MIT; Qwen2.5 1.5B uses Apache-2.0; Whisper and Silero use MIT. Optional pyannote segmentation uses MIT and 3D-Speaker embeddings Apache-2.0. Downloaded model terms remain applicable. Research-only Parakeet and third-party remake checkouts are not distributed.

The supplied Visual C++ redistributable and WebView2 bootstrapper are official Microsoft packages under their accompanying terms. Inno Setup builds the installers and is not embedded as a development tool. Build-only esbuild is MIT. Research scripts, snapshots and test audio are not product assets.
