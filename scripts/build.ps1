param([string[]]$Architectures = @('arm64','x64'))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $projectRoot 'tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet).Source }
Push-Location $projectRoot
try {
    & npm.cmd run build:orbs
    if ($LASTEXITCODE -ne 0) { throw 'Orb bundle build failed.' }
    foreach ($arch in $Architectures) {
        if ($arch -notin @('arm64','x64')) { throw 'Unsupported build architecture.' }
        $destination = Join-Path $projectRoot "artifacts/publish/win-$arch"
        $log = Join-Path $projectRoot "artifacts/publish-$arch.log"
        & $sdk publish src/Murmur -c Release -r "win-$arch" --self-contained true -p:PublishReadyToRun=true -o $destination *> $log
        if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 25; throw "Publish failed: $arch" }
        $engine = Join-Path $destination 'engines/llama'
        New-Item -ItemType Directory -Force -Path $engine | Out-Null
        $source = Join-Path $projectRoot "tools/llama-$arch"
        $names = @('llama-server.exe','llama-server-impl.dll','llama.dll','llama-common.dll','ggml.dll','ggml-base.dll','mtmd.dll','libomp.dll','LICENSE-LLVM-OpenMP')
        Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -in $names -or $_.Name -like 'ggml-cpu*.dll' } | Copy-Item -Destination $engine -Force
        # NuGet carries several architectures. Keep only this native platform's runtime files.
        $runtimes = Join-Path $destination 'runtimes'
        if (Test-Path -LiteralPath $runtimes) {
            foreach ($directory in Get-ChildItem -LiteralPath $runtimes -Directory) {
                $allowed = @("win-$arch"); if ($arch -eq 'x64') { $allowed += 'noavx' }
                if ($directory.Name -notin $allowed) {
                    $target = [IO.Path]::GetFullPath($directory.FullName)
                    if (!$target.StartsWith([IO.Path]::GetFullPath($destination) + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime cleanup escaped publish directory.' }
                    Remove-Item -LiteralPath $target -Recurse -Force
                }
            }
            if ($arch -eq 'x64') {
                $fallback = Join-Path $runtimes 'noavx'
                foreach ($directory in Get-ChildItem -LiteralPath $fallback -Directory -ErrorAction SilentlyContinue) {
                    if ($directory.Name -ne 'win-x64') {
                        $target = [IO.Path]::GetFullPath($directory.FullName)
                        if (!$target.StartsWith([IO.Path]::GetFullPath($destination) + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Fallback cleanup escaped publish directory.' }
                        Remove-Item -LiteralPath $target -Recurse -Force
                    }
                }
            }
        }
        Write-Output "Published native win-$arch"
        foreach ($document in @('USER-GUIDE.html','USER-GUIDE.md','LICENSE','THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $destination -Force }
        Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $destination -Recurse -Force
    }
} finally { Pop-Location }
