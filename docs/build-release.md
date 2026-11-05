# Build and release

Build on Windows 11 with .NET SDK 10.0.403 (ARM64 on this machine), Node 24, npm, Python 3 and Inno Setup 7.1.1. Production UI does not require Node or a development server. `.NET` is published self-contained. `package-lock.json`, NuGet lock files, model hashes and engine archives pin dependencies.

## Prepare

1. Put the .NET SDK in tools/dotnet, or install dotnet on PATH. The validated ARM64 archive is `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-arm64.zip`, SHA-512 `f78ecd8b48e24fd72ee604687b75f1bbb81505e8343f7d52e4baf9ea32372557eb30e88facaaad195bf2646fcce117fd941e49da0d5bdee1d60d63d836968429`. An x64 build host uses Microsoft's matching x64 SDK.
2. Run `npm ci` for the build-only orb bundler.
3. Download the two llama.cpp b12110 CPU archives listed with SHA-256 in engine-research.md. Verify the hashes, then extract into tools/llama-arm64 and tools/llama-x64. Include the accompanying LLVM OpenMP license. Do not use the research whisper CLI archives in a distributable: production uses Whisper.net's NuGet native runtime instead.
4. Install Inno Setup 7.1.1 into tools/inno or adjust the packaging script's compiler path. Validate the official installer Authenticode publisher Pyrsys B.V.
5. Put official Microsoft vc_redist.arm64.exe, vc_redist.x64.exe and MicrosoftEdgeWebview2Setup.exe in tools/prerequisites. Verify valid Microsoft Authenticode signatures. Validated package hashes for this build are in release-inputs.json. Evergreen prerequisite downloads can change: verify a new signed package and explicitly update the release input inventory rather than silently accepting different bytes.

## Build and check

Run from the project root in PowerShell:

```powershell
./tools/dotnet/dotnet.exe run --project tests/Murmur.Tests -c Release
./scripts/build.ps1
python scripts/verify-architecture.py
./scripts/package.ps1
```

`build.ps1` builds the licensed orb bundle, publishes both native self-contained architectures, isolates the correct inference dependencies, and copies guides/licenses. Logs go to artifacts/publish-*.log. `package.ps1` creates automatic, ARM64 and x64 installers. All native PE files are inventoried; managed architecture-neutral assemblies are allowed. Do not equate a successful publish with a physical-device test.

For development, set `DOTNET_ROOT` to tools/dotnet when launching framework-dependent executables directly. `MURMUR_DEVTOOLS=1` enables local WebView2 inspection on port 9332. This flag is off in normal installations. Keep testing profiles separate with `--data-dir <absolute path>`. Never ship private profile data or reference captures.

Additional tests: tests/Services.Integration takes a built Murmur.exe path and verifies DPAPI, provider event parsing, once-only reminders the actual stdio MCP process, and interrupted/resumed model transfers through a local HTTP fixture. tests/Windows.Integration exercises owned native and WebView2 fixture windows. Its owner-PID guard refuses all test input if focus belongs elsewhere. An active unlocked Windows desktop is required. Synthetic SAPI fixture generation is scripts/synthesize-fixtures.ps1; distinguish it from human speech when reporting quality.

## Public releases and updates

Public installers are hosted in [GitHub Releases](https://github.com/N3moStudio/Murmur/releases). This development release is unsigned, and Windows may show an unknown-publisher warning. SHA-256 integrity checks and any update-manifest signature are not Windows code signing. Public automatic app updating is not configured. The updater chooses RuntimeInformation.OSArchitecture, verifies a pinned RSA-PSS SHA-256 signature over the decoded payload bytes, then exact size and SHA-256 of the downloaded installer. It backs up SQLite before launching setup. A failed validation leaves the installed app intact. Installer failure is handled by Inno; automatic binary rollback after an installer has failed halfway is not implemented.

The signed JSON envelope has `payload` and `signature` as base64. The UTF-8 payload is a Release object: `version`, then `platforms` mapping `arm64`/`x64` to `url`, `sha256`, `bytes`. Supply the RSA public key in PEM SubjectPublicKeyInfo form. Keep the private release key outside this repository. Test on physical x64 and ARM64 before advertising broad compatibility. Calendar integrations separately require valid Google/Microsoft registrations and consent; no credentials are distributed.

## Source distribution

Include src/, tests/ (except bin/obj), third_party/, licenses/, scripts/, docs/ (excluding reference-images and evidence), installer/, package files, README, guides, LICENSE and this notice. Exclude tools/, node_modules/, artifacts/, research/, local user data, secrets and proprietary captures. Generated orbs.js is a distributable build asset; original licensed source is also included.

## Small architecture-detecting download

`installer/bootstrap-release.json` pins the version, GitHub repository and the SHA-256/size of both standalone installers. After producing and verifying those installers, update that record and run `./scripts/package-bootstrap.ps1`. It refuses to build if either local installer differs from the recorded hash/size.

The resulting `Murmur-Setup.exe` uses Inno's native OS `IsArm64` detection, including under x64 emulation. It downloads only the matching versioned GitHub asset, verifies its SHA-256 before execution, and opens the normal installer. It creates no second installed-app entry. The standalone installer retains all upgrade, data-preservation and uninstall behavior. The combined `-Setup-auto.exe` remains available as an alternative that carries both builds.

For a noninteractive download-only smoke check, run the helper with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DOWNLOADONLY=<empty-absolute-directory> /LOG=<absolute-log-path>`. This downloads/verifies the correct installer and saves it without executing it. Test a deliberately wrong hash using a separate test build and confirm failure leaves no saved installer. The physical x64 branch still needs an x64 machine.

Publish standalone assets before making the small downloader the primary link. GitHub's `/releases/latest/download/Murmur-Setup.exe` stays the stable user-facing URL when each future release includes that asset. Downloading the helper again gets the latest published helper; an old saved helper stays pinned to its original release.
