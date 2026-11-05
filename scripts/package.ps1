param([string]$Compiler = '', [string[]]$Targets = @('auto','arm64','x64'))
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if (!$Compiler) { $Compiler=Join-Path $projectRoot 'tools/inno/ISCC.exe' }
foreach($target in $Targets) {
 if ($target -notin @('auto','arm64','x64')) { throw 'Unsupported installer target.' }
 $log=Join-Path $projectRoot "artifacts/installer-$target.log"
 & $Compiler "/DTarget=$target" (Join-Path $projectRoot 'installer/Murmur.iss') *> $log
 if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $log -Tail 25;throw "Installer compilation failed: $target"}
 Write-Output "Built $target installer"
}
$releaseVersion = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src/Murmur/Murmur.csproj'))).Project.PropertyGroup.Version
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'artifacts/installers') -Filter "Murmur-$releaseVersion-Setup-*.exe" | ForEach-Object { [pscustomobject]@{file=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/installers/SHA256.json')
