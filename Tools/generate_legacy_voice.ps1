$ErrorActionPreference = 'Stop'
$voice = New-Object -ComObject SAPI.SpVoice
$voices = $voice.GetVoices()
for ($i=0; $i -lt $voices.Count; $i++) {
    if ($voices.Item($i).GetDescription() -like '*Heami*') { $voice.Voice = $voices.Item($i) }
}
$lines = @('밥 먹었어?', '다녀왔어.', '문 잠가.')
for ($i=0; $i -lt $lines.Count; $i++) {
    $stream = New-Object -ComObject SAPI.SpFileStream
    $stream.Open((Join-Path (Get-Location) "Assets/Resources/EchoLegacy$i.wav"), 3, $false)
    $voice.AudioOutputStream = $stream
    $voice.Speak($lines[$i]) | Out-Null
    $stream.Close()
}
Write-Output 'Generated 3 local synthetic legacy speech clips. No player recording used.'
