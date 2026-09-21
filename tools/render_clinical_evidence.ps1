param([string]$Run = 'clinical-evidence', [switch]$LaterCourse, [switch]$Room, [switch]$FullScript, [switch]$Independent, [string]$TestFilter = '')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $taskRoot ('artifacts/' + $Run)
New-Item -ItemType Directory -Path $output -Force | Out-Null
Import-Module (Join-Path $taskRoot 'app/Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$context = Enter-BotanicalGardenUnityValidationMirror -UnityExecutable 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ProjectPath (Join-Path $taskRoot 'app')
try {
    $arguments = @('-batchmode', '-buildTarget', 'Android', '-projectPath', ('"' + $context.MirrorRoot + '"'),
        '-executeMethod', $(if ($FullScript) { 'BotanicalGardenQR.Bootstrap.Editor.FullScriptJourneyPreview.Capture' } elseif ($Room) { 'BotanicalGardenQR.Bootstrap.Editor.ClinicalRoomVisualCheck.Capture' } elseif ($LaterCourse) { 'BotanicalGardenQR.Bootstrap.Editor.ClinicalCoursePreview.Capture' } else { 'BotanicalGardenQR.Bootstrap.Editor.ClinicalEvidencePreview.Capture' }),
        '-bgqrCaptureOutput', ('"' + $output + '"'), '-logFile', ('"' + (Join-Path $output 'render.log') + '"'))
    Write-Output ('Editor rendering only; Android target; output: ' + $output)
    if($Independent) { $arguments += '-bgqrIndependent' }
    $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    $renderExitCode = $process.ExitCode
    if ($TestFilter) {
        # Keep one mirror lease for rendering and targeted tests, avoiding a second asset import.
        $results = Join-Path $output 'tests.xml'
        $testArguments = @('-batchmode', '-nographics', '-buildTarget', 'Android', '-projectPath', ('"' + $context.MirrorRoot + '"'),
            '-runTests', '-testPlatform', 'EditMode', '-testFilter', ('"' + $TestFilter + '"'),
            '-testResults', ('"' + $results + '"'), '-logFile', ('"' + (Join-Path $output 'tests.log') + '"'))
        $tests = Start-Process -FilePath $context.UnityPath -ArgumentList $testArguments -PassThru -WindowStyle Hidden
        $tests.WaitForExit()
        if (-not (Test-Path -LiteralPath $results)) { throw 'Targeted EditMode report was not produced.' }
        [xml]$report = Get-Content -LiteralPath $results -Raw -Encoding UTF8
        $report.'test-run' | Select-Object result, total, passed, failed, duration | Format-List
        $report.SelectNodes('//test-case[failure]') | ForEach-Object { $_.fullname; $_.failure.InnerText }
        if ($tests.ExitCode -ne 0 -or [int]$report.'test-run'.failed -ne 0) { throw "Targeted EditMode checks failed. See $results" }
    }
    if ($renderExitCode -ne 0) { throw "Editor render exited $renderExitCode. See $output/render.log" }
}
finally { Exit-BotanicalGardenUnityValidationMirror -Context $context }
