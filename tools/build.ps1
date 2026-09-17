param(
    [ValidateSet('Prepare','Test','Quest')][string]$Target='Prepare',
    [string]$Unity='D:\unityhub\unity22.3.62f3c1\6000.3.23f1\Editor\Unity.exe'
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if ($projectRoot -match '[^\x00-\x7F]') {
    # Android's Unity preflight rejects non-ASCII paths. A verified junction keeps one source project.
    $asciiPath=Join-Path (Split-Path -Parent $projectRoot) 'EndoscopyBuild'
    if (Test-Path -LiteralPath $asciiPath) {
        $link=Get-Item -LiteralPath $asciiPath
        if ($link.LinkType -ne 'Junction' -or $link.Target -ne $projectRoot) { throw "Build alias points elsewhere: $asciiPath" }
    } else { New-Item -ItemType Junction -Path $asciiPath -Target $projectRoot | Out-Null }
    $projectRoot=$asciiPath
}
$appPath=Join-Path $projectRoot 'app'
$artifactPath=Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
if (!(Test-Path -LiteralPath $Unity)) { throw "Unity executable not found: $Unity" }
$logPath=Join-Path $artifactPath ($Target.ToLower()+'.log')
$unityArgs=@('-batchmode','-buildTarget','Android','-nographics','-projectPath',('"'+$appPath+'"'),'-logFile',('"'+$logPath+'"'))
if ($Target -eq 'Test') {
    $unityArgs+=@('-runTests','-testPlatform','EditMode','-testResults',('"'+(Join-Path $artifactPath 'editmode-results.xml')+'"'))
} else {
    $method=@{Prepare='EndoscopyTheme.Editor.EndoscopyThemeSetup.Apply';Quest='BotanicalGardenQR.Editor.Build.BotanicalGardenAndroidBuild.Build'}[$Target]
    $unityArgs+=@('-quit','-executeMethod',$method)
}
$process=Start-Process -FilePath $Unity -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
# Wait for the editor itself; Unity may leave shader workers alive after a successful build.
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)); read $logPath" }
Write-Output "Completed $Target. Log: $logPath"
