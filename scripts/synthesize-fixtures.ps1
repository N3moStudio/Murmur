$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path $PSScriptRoot '../artifacts/test-audio'
$voice = New-Object -ComObject SAPI.SpVoice
$voices = $voice.GetVoices()
$cases = @(
 @{ Name='numbers-negation'; Text='Please do not cancel the meeting. Move it to November fourth at three thirty. Send thirty eight invoices, not thirty seven. The total is two hundred and forty nine dollars and fifty cents.'; Voice=0 },
 @{ Name='email-correction'; Text='Hi Sarah. We can meet on November fourth at three thirty. Please send the thirty seven invoices. Actually, make that thirty eight invoices. Thanks, Nathan.'; Voice=1 },
 @{ Name='short-answer'; Text='No, not yet.'; Voice=1 },
 @{ Name='meeting-alex-one'; Text='I am Alex. We agreed to ship the desktop version on Friday. I will prepare the installer.'; Voice=0 },
 @{ Name='meeting-sam'; Text='I am Sam. I will test the keyboard shortcut and send the results by Thursday.'; Voice=1 },
 @{ Name='meeting-alex-two'; Text='Thank you Sam. The budget is two hundred fifty dollars. We must not record meetings automatically.'; Voice=0 }
)
foreach ($case in $cases) {
 $stream = New-Object -ComObject SAPI.SpFileStream
 $stream.Format.Type = 18
 $stream.Open((Join-Path $fixtureRoot ($case.Name + '.wav')), 3, $false)
 try { $voice.Voice = $voices.Item([Math]::Min($case.Voice, $voices.Count - 1)); $voice.AudioOutputStream = $stream; [void]$voice.Speak($case.Text) }
 finally { $stream.Close(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($stream) }
}
[void][Runtime.InteropServices.Marshal]::ReleaseComObject($voice)
$cases | ConvertTo-Json | Set-Content (Join-Path $fixtureRoot 'synthetic-fixtures.json')
