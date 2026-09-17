param([string]$Adb='D:\platform-tools\adb.exe',[string]$Serial,[switch]$Launch)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$apkPath=Join-Path $projectRoot 'builds\EndoscopyInspection-Quest3.apk'
if(!(Test-Path -LiteralPath $Adb)){throw "ADB not found: $Adb. Pass -Adb with your adb.exe path."}
if(!(Test-Path -LiteralPath $apkPath)){throw "APK not found: $apkPath"}
if(!$Serial){
    $devices=@(& $Adb devices | Where-Object {$_ -match '^\S+\s+device$'} | ForEach-Object {($_ -split '\s+')[0]})
    if($devices.Count -ne 1){throw 'Connect exactly one authorized Quest, or specify -Serial. Allow USB debugging inside the headset.'}
    $Serial=$devices[0]
}
& $Adb -s $Serial install -r $apkPath
if($LASTEXITCODE -ne 0){throw 'APK installation failed.'}
if($Launch){
    & $Adb -s $Serial shell am start -n 'com.endoscopy.inspection/com.unity3d.player.UnityPlayerGameActivity'
    if($LASTEXITCODE -ne 0){throw 'Launch request failed.'}
}
Write-Output 'Installation complete. Enable hand tracking on Quest, put controllers down, and hold your hands in front of the headset.'
