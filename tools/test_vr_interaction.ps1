param(
    [string]$TestFilter = 'AuthoredFirstLegMovesActualFairyToFirstPoint;VisitorDialoguePointableLifecycleTests;VisitorDialogueClickCommitGateTests;ActualMetaSurfaceCatchesFingerSweepAndRejectsPointerSubmit;SilentVisitorAudioTests;BotanicalGardenQR.MapNavigation.Tests',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')]
    [string]$Run = 'current',
    [switch]$CaptureStationary,
    [switch]$PrepareStationaryEntry,
    [switch]$PrepareWaitingRoom,
    [switch]$PrepareStorageRoom
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'app'
$output = Join-Path $root 'artifacts/vr-interaction'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Import-Module (Join-Path $app 'Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$context = Enter-BotanicalGardenUnityValidationMirror -UnityExecutable 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ProjectPath $app
try {
    if ($PrepareStorageRoom) {
        $storageOutput = Join-Path $root 'artifacts/storage-room'
        New-Item -ItemType Directory -Path $storageOutput -Force | Out-Null
        $arguments = @('-batchmode', '-nographics', '-buildTarget', 'Android', '-projectPath', ('"' + $context.MirrorRoot + '"'),
            '-executeMethod', 'BotanicalGardenQR.Bootstrap.Editor.StorageRoomPublish.Publish', '-logFile', ('"' + (Join-Path $storageOutput 'publish.log') + '"'))
        $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Storage room publication failed; inspect $storageOutput/publish.log" }
        $storageRelative = 'Assets/EndoscopyTheme/Resources/FullScriptRooms/Storage'
        $storageTarget = Join-Path $app $storageRelative
        New-Item -ItemType Directory -Path $storageTarget -Force | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $context.MirrorRoot $storageRelative) -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $storageTarget $_.Name) -Force
        }
        Copy-Item -LiteralPath ((Join-Path $context.MirrorRoot $storageRelative) + '.meta') -Destination ($storageTarget + '.meta') -Force
    }
    if ($PrepareWaitingRoom) {
        $waitingOutput = Join-Path $root 'artifacts/waiting-room'
        New-Item -ItemType Directory -Path $waitingOutput -Force | Out-Null
        $arguments = @('-batchmode', '-nographics', '-buildTarget', 'Android', '-projectPath', ('"' + $context.MirrorRoot + '"'),
            '-executeMethod', 'BotanicalGardenQR.Bootstrap.Editor.WaitingRoomPublish.Publish', '-logFile', ('"' + (Join-Path $waitingOutput 'publish.log') + '"'))
        $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Waiting room publication failed; inspect $waitingOutput/publish.log" }
        $waitingRelative = 'Assets/EndoscopyTheme/Resources/FullScriptRooms/Waiting'
        $waitingTarget = Join-Path $app $waitingRelative
        New-Item -ItemType Directory -Path $waitingTarget -Force | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $context.MirrorRoot $waitingRelative) -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $waitingTarget $_.Name) -Force
        }
        Copy-Item -LiteralPath ((Join-Path $context.MirrorRoot $waitingRelative) + '.meta') -Destination ($waitingTarget + '.meta') -Force
    }
    if ($PrepareStationaryEntry) {
        $entryOutput = Join-Path $root 'artifacts/stationary-entry'
        New-Item -ItemType Directory -Path $entryOutput -Force | Out-Null
        $arguments = @('-batchmode', '-nographics', '-buildTarget', 'Android', '-projectPath', ('"' + $context.MirrorRoot + '"'),
            '-executeMethod', 'BotanicalGardenQR.Bootstrap.Editor.StationaryEntryPublish.Publish',
            '-bgqrEntryOutput', ('"' + $entryOutput + '"'), '-logFile', ('"' + (Join-Path $entryOutput 'publish.log') + '"'))
        $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Stationary entry dependency gate failed; inspect $entryOutput/publish.log" }
        $relativePrefab = 'Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab'
        Copy-Item -LiteralPath (Join-Path $context.MirrorRoot $relativePrefab) -Destination (Join-Path $app $relativePrefab) -Force
        $entryArchive = Join-Path $root 'archive/visitor-entry-20260921'
        New-Item -ItemType Directory -Path $entryArchive -Force | Out-Null
        $entryBackup = Join-Path $entryArchive 'VisitorRuntime.before-dependency-isolation.prefab'
        if (!(Test-Path -LiteralPath $entryBackup)) {
            Copy-Item -LiteralPath (Join-Path $entryOutput 'VisitorRuntime.before-dependency-isolation.prefab') -Destination $entryBackup
        }
    }
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
    if ($CaptureStationary) {
        $captureOutput = Join-Path $root 'artifacts/stationary-final'
        New-Item -ItemType Directory -Path $captureOutput -Force | Out-Null
        $arguments = @('-batchmode', '-buildTarget', 'Android', '-force-vulkan', '-projectPath', ('"' + $context.MirrorRoot + '"'),
            '-executeMethod', 'BotanicalGardenQR.Bootstrap.Editor.FullScriptJourneyPreview.CaptureStationary',
            '-bgqrCaptureOutput', ('"' + $captureOutput + '"'), '-logFile', ('"' + (Join-Path $captureOutput 'preview.log') + '"'))
        $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Stationary preview failed; inspect $captureOutput/preview.log" }
        & (Join-Path $PSScriptRoot 'check_storage_register_capture.ps1') -Directory $captureOutput
    }
}
finally { Exit-BotanicalGardenUnityValidationMirror -Context $context }
