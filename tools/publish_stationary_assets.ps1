param([switch]$Preview)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $taskRoot 'app/Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$context=Enter-BotanicalGardenUnityValidationMirror -UnityExecutable 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ProjectPath (Join-Path $taskRoot 'app')
try {
    $output=Join-Path $taskRoot 'artifacts/stationary'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $arguments=@('-batchmode','-buildTarget','Android','-projectPath',('"'+$context.MirrorRoot+'"'),'-executeMethod','BotanicalGardenQR.Bootstrap.Editor.StationaryAssetPublish.Publish','-logFile',('"'+(Join-Path $output 'publish.log')+'"'))
    $process=Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw 'Stationary asset publication failed; inspect artifacts/stationary/publish.log'}
    $relative='Assets/EndoscopyTheme/Resources/FullScriptRooms/Stationary'
    $source=Join-Path $context.MirrorRoot $relative
    $target=Join-Path $taskRoot ('app/'+$relative)
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath $source -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $_.Name) -Force}
    Copy-Item -LiteralPath ($source+'.meta') -Destination ($target+'.meta') -Force
    $renderer='Assets/Universal Render Pipeline Asset_Renderer.asset'
    Copy-Item -LiteralPath (Join-Path $context.MirrorRoot $renderer) -Destination (Join-Path $taskRoot ('app/'+$renderer)) -Force
    Write-Output 'Published supplied lobby and prop references plus Gaussian renderer feature; no APK build.'
    if($Preview)
    {
        $arguments=@('-batchmode','-buildTarget','Android','-force-vulkan','-projectPath',('"'+$context.MirrorRoot+'"'),'-executeMethod','BotanicalGardenQR.Bootstrap.Editor.FullScriptJourneyPreview.CaptureStationary','-bgqrCaptureOutput',('"'+$output+'"'),'-logFile',('"'+(Join-Path $output 'preview.log')+'"'))
        $process=Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $process.WaitForExit()
        if($process.ExitCode -ne 0){throw 'Production stationary preview failed; inspect artifacts/stationary/preview.log'}
    }
}
finally{Exit-BotanicalGardenUnityValidationMirror -Context $context}
