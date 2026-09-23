param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Run = 'current',
    [switch]$EmptyScene,
    [switch]$Vulkan
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'artifacts/production-play'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close the existing Unity editor before running the production Play probe.' }
$taskUnity = 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe'
$taskLog = Join-Path $taskOutput ($Run + '.log')
$taskResult = Join-Path $taskOutput ($Run + '.txt')
if (Test-Path -LiteralPath $taskResult) { throw 'Use a new run name to preserve previous evidence.' }
$taskArgs = @('-batchmode', '-buildTarget', 'Android', '-projectPath', '"D:/quest3/EndoscopyBuild/app"',
    '-executeMethod', 'BotanicalGardenQR.Bootstrap.Editor.ProductionPlayProbe.Run',
    '-endoscopyProbeOutput', ('"' + $taskResult + '"'), '-logFile', ('"' + $taskLog + '"'))
if ($EmptyScene) { $taskArgs += '-endoscopyProbeEmpty' }
if ($Vulkan) { $taskArgs += '-force-vulkan' }
$taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -PassThru -WindowStyle Hidden
Write-Output ('Unity probe PID: ' + $taskProcess.Id)
$taskDeadline = [DateTime]::UtcNow.AddMinutes(8)
while (!$taskProcess.HasExited) {
    if ([DateTime]::UtcNow -gt $taskDeadline) { $taskProcess.Kill(); throw 'Play probe timed out; inspect its log.' }
    Start-Sleep -Seconds 1
    $taskProcess.Refresh()
}
if (Test-Path -LiteralPath $taskResult) { Get-Content -LiteralPath $taskResult }
Write-Output ('Unity exit code: ' + $taskProcess.ExitCode)
$taskGraphicsFailure = Select-String -LiteralPath $taskLog -Pattern 'requires features which are unavailable|Kernel.*is invalid|Shader error|Failed to create global context|NullReferenceException|MissingReferenceException' -Quiet
if ($taskProcess.ExitCode -ne 0 -or $taskGraphicsFailure -or !(Test-Path -LiteralPath $taskResult) -or !(Select-String -LiteralPath $taskResult -Pattern ' PASS$' -Quiet)) {
    throw ('Production Play failed: ' + $taskLog)
}
