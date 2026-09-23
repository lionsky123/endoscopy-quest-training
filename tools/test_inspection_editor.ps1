param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Run,
    [Parameter(Mandatory=$true)][string]$TestFilter,
    [switch]$Graphics
)
$ErrorActionPreference='Stop'
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity is already running.'}
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskDir=Join-Path $taskRoot 'artifacts/inspection-editor'
New-Item -ItemType Directory -Path $taskDir -Force | Out-Null
$taskReport=Join-Path $taskDir ($Run+'.xml')
if(Test-Path -LiteralPath $taskReport){throw 'Use a new run name to preserve evidence.'}
$taskArgs=@('-batchmode','-buildTarget','Android','-projectPath','"D:/quest3/EndoscopyBuild/app"',
    '-runTests','-testPlatform','EditMode','-testFilter',('"'+$TestFilter+'"'),
    '-testResults',('"'+$taskReport+'"'),'-logFile',('"'+(Join-Path $taskDir ($Run+'.log'))+'"'))
if($Graphics){$taskArgs+='-force-vulkan'}else{$taskArgs+='-nographics'}
$taskProcess=Start-Process 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ArgumentList $taskArgs -PassThru -WindowStyle Hidden
Write-Output ('Unity tests PID: '+$taskProcess.Id)
$taskDeadline=[DateTime]::UtcNow.AddMinutes(12)
while(!$taskProcess.HasExited){
    if([DateTime]::UtcNow -gt $taskDeadline){$taskProcess.Kill();throw 'Editor test timeout.'}
    Start-Sleep -Seconds 1
    $taskProcess.Refresh()
}
if(!(Test-Path -LiteralPath $taskReport)){throw 'No test report; inspect log.'}
[xml]$taskResult=Get-Content -LiteralPath $taskReport -Raw -Encoding UTF8
$taskResult.'test-run' | Select-Object result,total,passed,failed,duration
$taskResult.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {Write-Output ($_.fullname+': '+$_.failure.InnerText)}
if($taskProcess.ExitCode -ne 0 -or $taskResult.'test-run'.result -ne 'Passed'){throw 'Editor checks failed.'}
