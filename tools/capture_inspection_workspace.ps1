param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Run='current')
$ErrorActionPreference='Stop'
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity is already running.'}
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot ('artifacts/inspection-workspace/'+$Run)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a new run name to preserve previous captures.'}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskLog=Join-Path $taskOutput 'editor.log'
$taskArgs=@('-batchmode','-force-vulkan','-buildTarget','Android','-projectPath','"D:/quest3/EndoscopyBuild/app"',
 '-executeMethod','BotanicalGardenQR.Bootstrap.Editor.InspectionWorkspace.CaptureRoomPreviews',
 '-inspectionPreviewOutput',('"'+$taskOutput+'"'),'-quit','-logFile',('"'+$taskLog+'"'))
$taskProcess=Start-Process 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ArgumentList $taskArgs -PassThru -WindowStyle Hidden
if(!$taskProcess.WaitForExit(360000)){$taskProcess.Kill();throw 'Workspace capture timed out.'}
if($taskProcess.ExitCode -ne 0){throw 'Workspace capture failed; inspect editor.log.'}
foreach($taskRoom in @('R00_LOBBY','R01_OFFICE')){foreach($taskPose in @('seated','standing')){
 $taskImage=Join-Path $taskOutput ($taskRoom+'-'+$taskPose+'.png')
 if(!(Test-Path -LiteralPath $taskImage)){throw ('Missing capture: '+$taskImage)}
 Write-Output $taskImage
}}
if(Select-String -LiteralPath $taskLog -Pattern 'Shader error|NullReferenceException|MissingReferenceException|requires features which are unavailable|Kernel.*is invalid' -Quiet){throw 'Workspace graphics errors; inspect editor.log.'}
