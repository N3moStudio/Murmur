# Contributing to Murmur

Please read the [feature checklist](docs/parity.md) and [build instructions](docs/build-release.md). Keep Windows and Mac implementation status explicit. A compiled build is not evidence of physical-device behavior.

For Windows, run the core checks with `dotnet run --project tests/Murmur.Tests -c Release` and the download controller checks with `node --test tests/Web/model-setup.test.mjs`. Building installers additionally requires the pinned native engines, Microsoft prerequisites and Inno Setup listed in the build instructions. Do not bundle downloaded model weights or personal profiles in source commits.

For Mac, use `bash macos/scripts/build.sh` on Apple silicon with Xcode 26 or newer. Treat the preview as unfinished until compilation, permission, audio and insertion tests pass on a Mac.

Keep changes focused and preserve private data, cancelled recordings, clipboard ownership and text already edited by the user. Never insert or submit into an unrelated field. Document measured latency with hardware, audio, warm/cold state and which processing stages were included.

Report reproducible bugs through GitHub Issues. Remove private transcripts, credentials and personal paths from reports. Do not include anyone's audio without their permission. Include Windows/macOS version, CPU architecture, Murmur version, steps and any non-sensitive error text.

Contributions to original project code are provided under the MIT license. Keep upstream copyright and license notices with reused components.
