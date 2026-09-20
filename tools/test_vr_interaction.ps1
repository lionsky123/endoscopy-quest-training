param(
    [string]$TestFilter = 'AuthoredFirstLegMovesActualFairyToFirstPoint;VisitorDialoguePointableLifecycleTests;VisitorDialogueClickCommitGateTests;ActualMetaSurfaceCatchesFingerSweepAndRejectsPointerSubmit;SilentVisitorAudioTests;BotanicalGardenQR.MapNavigation.Tests',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')]
    [string]$Run = 'current'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'app'
$output = Join-Path $root 'artifacts/vr-interaction'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Import-Module (Join-Path $app 'Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$context = Enter-BotanicalGardenUnityValidationMirror -UnityExecutable 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ProjectPath $app
try {
    $results = Join-Path $output ($Run + '.xml')
    $log = Join-Path $output ($Run + '.log')
    # A stale report must never be accepted as evidence for this invocation.
    if (Test-Path -LiteralPath $results) { Remove-Item -LiteralPath $results -Force }
    $arguments = @('-batchmode', '-nographics', '-buildTarget', 'Android', '-projectPath', ('"' + $context.MirrorRoot + '"'),
        '-runTests', '-testPlatform', 'EditMode', '-testFilter', ('"' + $TestFilter + '"'),
        '-testResults', ('"' + $results + '"'), '-logFile', ('"' + $log + '"'))
    Write-Output ('EditMode only; Android target; results: ' + $results)
    $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "EditMode tests exited $($process.ExitCode). See $log" }
    if (!(Test-Path -LiteralPath $results -PathType Leaf)) { throw "EditMode did not produce a fresh test report. See $log" }
    [xml]$report = Get-Content -LiteralPath $results -Raw -Encoding UTF8
    $runResult = $report.'test-run'
    $runResult | Select-Object result, total, passed, failed, duration | Format-List
    $report.SelectNodes('//test-case[failure]') | ForEach-Object { $_.fullname; $_.failure.InnerText }
    if ($null -eq $runResult -or $runResult.result -ne 'Passed' -or [int]$runResult.total -le 0 -or [int]$runResult.failed -ne 0) {
        throw "EditMode report is not a passing, non-empty run. See $results"
    }
}
finally { Exit-BotanicalGardenUnityValidationMirror -Context $context }
