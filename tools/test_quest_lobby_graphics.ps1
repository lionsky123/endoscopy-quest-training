param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Run,[string]$TestFilter='StationaryScriptTests.DefaultLobbyBindsPanoramaWithoutGaussianOrPipelineOverride|StationaryScriptTests.VrBuildDoesNotRegisterMrukNativeOrGlobalUpdateStartup')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot 'artifacts/quest-lobby-20260923'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskReport=Join-Path $taskOutput ($Run+'.xml')
if(Test-Path -LiteralPath $taskReport){throw 'Choose a fresh run name.'}
Import-Module (Join-Path $taskRoot 'app/Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$taskContext=Enter-BotanicalGardenUnityValidationMirror -UnityExecutable 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ProjectPath (Join-Path $taskRoot 'app')
try {
 $taskArgs=@('-batchmode','-force-vulkan','-buildTarget','Android','-projectPath',('"'+$taskContext.MirrorRoot+'"'),'-runTests','-testPlatform','EditMode','-testFilter',('"'+$TestFilter+'"'),'-testResults',('"'+$taskReport+'"'),'-logFile',('"'+(Join-Path $taskOutput ($Run+'.log'))+'"'))
 $taskArgs+=@('-questLobbyOutput',('"'+(Join-Path $taskOutput $Run)+'"'))
 $taskProcess=Start-Process $taskContext.UnityPath -ArgumentList $taskArgs -PassThru -WindowStyle Hidden
 if(!$taskProcess.WaitForExit(600000)){$taskProcess.Kill();throw 'Graphics regression timed out.'}
 if(!(Test-Path -LiteralPath $taskReport)){throw 'Missing graphics test report.'}
 [xml]$taskResult=Get-Content -LiteralPath $taskReport -Raw
 $taskResult.'test-run' | Select-Object result,total,passed,failed
 $taskResult.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {$_.failure.InnerText}
 if($taskProcess.ExitCode -ne 0 -or $taskResult.'test-run'.result -ne 'Passed'){throw 'Graphics regression failed.'}
} finally { Exit-BotanicalGardenUnityValidationMirror -Context $taskContext }
