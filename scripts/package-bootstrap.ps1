param([string]$Compiler = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$Compiler) { $Compiler = Join-Path $projectRoot 'tools/inno/ISCC.exe' }
$release = Get-Content -LiteralPath (Join-Path $projectRoot 'installer/bootstrap-release.json') -Raw | ConvertFrom-Json
if ($release.repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $release.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release identity.' }
foreach ($arch in @('arm64', 'x64')) {
    $entry = $release.platforms.$arch
    if ($entry.sha256 -notmatch '^[a-f0-9]{64}$' -or $entry.bytes -le 0 -or $entry.file -ne "Murmur-$($release.version)-Setup-$arch.exe") { throw "Invalid $arch release record." }
    $file = Join-Path $projectRoot "artifacts/installers/$($entry.file)"
    if ((Get-Item -LiteralPath $file).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Rebuild the release record: $arch installer differs." }
}
$arguments = @("/DAppVersion=$($release.version)", "/DRepository=$($release.repository)",
    "/DArmBytes=$($release.platforms.arm64.bytes)", "/DArmHash=$($release.platforms.arm64.sha256)",
    "/DX64Bytes=$($release.platforms.x64.bytes)", "/DX64Hash=$($release.platforms.x64.sha256)",
    (Join-Path $projectRoot 'installer/Bootstrap.iss'))
$log = Join-Path $projectRoot 'artifacts/installer-bootstrap.log'
& $Compiler @arguments *> $log
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 25; throw 'Bootstrap installer build failed.' }
Write-Output 'Built Murmur-Setup.exe with native-OS architecture detection and pinned SHA-256 checks.'
