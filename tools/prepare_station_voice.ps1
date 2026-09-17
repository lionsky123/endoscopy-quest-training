$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$stationRoot=Split-Path -Parent $PSScriptRoot
$lessons=Get-Content -LiteralPath (Join-Path $stationRoot 'app/Assets/Endoscopy/Resources/station-lessons.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$voiceFolder=Join-Path $stationRoot 'app/Assets/Endoscopy/Resources/StationVoice'
New-Item -ItemType Directory -Path $voiceFolder -Force | Out-Null
$reader=New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $reader.SelectVoice('Microsoft Huihui Desktop')
    $reader.Rate=-1
    foreach($step in $lessons.steps){
        $reader.SetOutputToWaveFile((Join-Path $voiceFolder ($step.id+'.wav')))
        $reader.Speak($step.narration)
        $reader.SetOutputToNull()
        $reader.SetOutputToWaveFile((Join-Path $voiceFolder ($step.id+'-feedback.wav')))
        $reader.Speak($step.explanation)
        $reader.SetOutputToNull()
    }
    $reader.SetOutputToWaveFile((Join-Path $voiceFolder 'welcome.wav'))
    $reader.Speak('欢迎进入清洗消毒室。今天分六站学习。请在实际可用空间内转身或小范围走近。用手柄指向工位按钮并按扳机选择，左手X可以重听提示。每一步先查看证据，再完成核查；最后主动进入下一站。请先选择开始第一站。')
    $reader.SetOutputToNull()
    $reader.SetOutputToWaveFile((Join-Path $voiceFolder 'assessment.wav'))
    $reader.Speak('请观察当前工位。若有多个观察位置，先逐一查看。打开本步证据并翻阅全部资料，再记录判断。当前情境不会提示正确答案。完成后主动继续。')
    $reader.SetOutputToNull()
} finally { $reader.Dispose() }
Write-Output 'Prepared offline Chinese station narration.'
