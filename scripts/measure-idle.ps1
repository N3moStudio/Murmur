param([int]$RootProcessId, [string]$Output, [int]$Seconds = 20)
$ErrorActionPreference='Stop'
function Get-TreeIds {
 $processes=Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId
 $ids=[Collections.Generic.HashSet[int]]::new();[void]$ids.Add($RootProcessId)
 do { $added=$false;foreach($process in $processes){if($ids.Contains([int]$process.ParentProcessId) -and $ids.Add([int]$process.ProcessId)){$added=$true}} } while($added)
 return @($ids)
}
$ids=Get-TreeIds
$before=@{};foreach($processId in $ids){$process=Get-Process -Id $processId -ErrorAction SilentlyContinue;if($process){$before[$processId]=$process.TotalProcessorTime.TotalSeconds}}
$watch=[Diagnostics.Stopwatch]::StartNew();Start-Sleep -Seconds $Seconds
$cpu=0;$working=0L;$private=0L;$names=@()
foreach($processId in (Get-TreeIds)){
 $process=Get-Process -Id $processId -ErrorAction SilentlyContinue;if(!$process){continue}
 if($before.ContainsKey($processId)){$cpu += $process.TotalProcessorTime.TotalSeconds-$before[$processId]}
 $working += $process.WorkingSet64;$private += $process.PrivateMemorySize64;$names += $process.ProcessName
}
$result=[pscustomobject]@{seconds=$watch.Elapsed.TotalSeconds;logicalProcessors=[Environment]::ProcessorCount;cpuPercent=[Math]::Round(100*$cpu/$watch.Elapsed.TotalSeconds/[Environment]::ProcessorCount,4);workingSetMB=[Math]::Round($working/1MB,1);privateMB=[Math]::Round($private/1MB,1);processes=$names}
$result | ConvertTo-Json | Set-Content -LiteralPath $Output
$result | Format-List
