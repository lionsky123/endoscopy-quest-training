param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Run = 'current',
    [switch]$EmptyScene,
    [switch]$Vulkan,
    [switch]$MissingInitialLobby,
    [switch]$RouteScreens
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'artifacts/production-play'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close the existing Unity editor before running the production Play probe.' }
if ($EmptyScene -and $MissingInitialLobby) { throw 'Choose either an empty scene or a missing initial lobby probe.' }
$taskUnity = 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe'
$taskLog = Join-Path $taskOutput ($Run + '.log')
$taskResult = Join-Path $taskOutput ($Run + '.txt')
if (Test-Path -LiteralPath $taskResult) { throw 'Use a new run name to preserve previous evidence.' }
$taskLobby = Join-Path $taskRoot 'app/Assets/EndoscopyTheme/Resources/FullScriptRooms/Stationary/LobbyPanorama.prefab'
$taskLobbyMeta = $taskLobby + '.meta'
$taskUnavailableLobby = Join-Path $taskOutput 'LobbyPanorama.UnavailableForPlay.prefab'
$taskUnavailableLobbyMeta = $taskUnavailableLobby + '.meta'
$taskLobbyMoved = $false
$taskLobbyMetaMoved = $false
try {
    if ($MissingInitialLobby) {
        if (!(Test-Path -LiteralPath $taskLobby -PathType Leaf) -or !(Test-Path -LiteralPath $taskLobbyMeta -PathType Leaf)) {
            throw 'The real lobby panorama prefab and its meta file are both required for safe fault injection.'
        }
        if ((Test-Path -LiteralPath $taskUnavailableLobby -PathType Leaf) -or (Test-Path -LiteralPath $taskUnavailableLobbyMeta -PathType Leaf)) {
            throw 'The temporary lobby backup path is already occupied.'
        }
        Move-Item -LiteralPath $taskLobby -Destination $taskUnavailableLobby
        $taskLobbyMoved = $true
        Move-Item -LiteralPath $taskLobbyMeta -Destination $taskUnavailableLobbyMeta
        $taskLobbyMetaMoved = $true
        Write-Output 'Temporarily withheld the real LobbyPanorama prefab for the first-room failure probe.'
    }

    $taskArgs = @('-batchmode', '-buildTarget', 'Android', '-projectPath', '"D:/quest3/EndoscopyBuild/app"',
        '-executeMethod', 'BotanicalGardenQR.Bootstrap.Editor.ProductionPlayProbe.Run',
        '-endoscopyProbeOutput', ('"' + $taskResult + '"'), '-logFile', ('"' + $taskLog + '"'))
    if ($EmptyScene) { $taskArgs += '-endoscopyProbeEmpty' }
    if ($MissingInitialLobby) { $taskArgs += '-endoscopyProbeMissingInitialRoom' }
    if ($RouteScreens) { $taskArgs += '-endoscopyProbeRouteScreens' }
    if ($Vulkan) { $taskArgs += '-force-vulkan' }
    $taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -PassThru -WindowStyle Hidden
    Write-Output ('Unity probe PID: ' + $taskProcess.Id)
    $taskDeadline = [DateTime]::UtcNow.AddMinutes(8)
    while (!$taskProcess.HasExited) {
        if ([DateTime]::UtcNow -gt $taskDeadline) {
            $taskProcess.Kill()
            $taskProcess.WaitForExit()
            throw 'Play probe timed out; inspect its log.'
        }
        Start-Sleep -Seconds 1
        $taskProcess.Refresh()
    }
    if (Test-Path -LiteralPath $taskResult) { Get-Content -LiteralPath $taskResult }
    Write-Output ('Unity exit code: ' + $taskProcess.ExitCode)
    $taskRuntimeFailure = Select-String -LiteralPath $taskLog -Pattern 'requires features which are unavailable|Kernel.*is invalid|Shader error|Failed to create global context|AudioClip\.SetData failed|NullReferenceException|MissingReferenceException' -Quiet
    if ($taskProcess.ExitCode -ne 0 -or $taskRuntimeFailure -or !(Test-Path -LiteralPath $taskResult) -or !(Select-String -LiteralPath $taskResult -Pattern ' PASS$' -Quiet)) {
        throw ('Production Play failed: ' + $taskLog)
    }
}
finally {
    if ($taskLobbyMoved -and (Test-Path -LiteralPath $taskUnavailableLobby -PathType Leaf)) {
        if (Test-Path -LiteralPath $taskLobby -PathType Leaf) { throw 'Cannot restore the lobby prefab because the original path is occupied.' }
        Move-Item -LiteralPath $taskUnavailableLobby -Destination $taskLobby
    }
    if ($taskLobbyMetaMoved -and (Test-Path -LiteralPath $taskUnavailableLobbyMeta -PathType Leaf)) {
        if (Test-Path -LiteralPath $taskLobbyMeta -PathType Leaf) { throw 'Cannot restore the lobby meta file because the original path is occupied.' }
        Move-Item -LiteralPath $taskUnavailableLobbyMeta -Destination $taskLobbyMeta
    }
    if ($taskLobbyMoved -and (!(Test-Path -LiteralPath $taskLobby -PathType Leaf) -or !(Test-Path -LiteralPath $taskLobbyMeta -PathType Leaf))) {
        throw 'The real lobby panorama asset and meta file were not restored.'
    }
}
