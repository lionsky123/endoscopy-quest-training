$ErrorActionPreference='Stop'
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity is already running. Use Endoscopy > 当前游戏工作区 in that editor, or close it before reopening with Vulkan.'}
$taskArgs=@('-buildTarget','Android','-force-vulkan','-projectPath','"D:/quest3/EndoscopyBuild/app"',
    '-executeMethod','BotanicalGardenQR.Bootstrap.Editor.InspectionWorkspace.Open')
Start-Process 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ArgumentList $taskArgs | Out-Null
