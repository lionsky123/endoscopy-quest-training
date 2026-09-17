param(
    [string]$InstalledApk,
    [string]$CandidateApk,
    [string]$OutputPath,
    [string]$Adb='D:\unityhub\unity22.3.62f3c1\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$CandidateApk){
    $receiptPath=Join-Path $root 'artifacts/latest-quest-build.json'
    if(!(Test-Path -LiteralPath $receiptPath)){throw '没有新构建记录，请显式传入 -CandidateApk 要核对的 APK；不把 1.apk 自动当成最新版。'}
    $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json
    $CandidateApk=$receipt.apkPath
    if(!$CandidateApk -or !(Test-Path -LiteralPath $CandidateApk)){throw '构建记录对应的 APK 不存在。'}
    if((Get-FileHash -LiteralPath $CandidateApk -Algorithm SHA256).Hash -ne $receipt.apkSha256){throw 'APK 与构建记录哈希不符，请显式选择要核对的文件。'}
}
$deviceVerified=$false
if(!$InstalledApk){
    $remote=@(& $Adb shell pm path com.endoscopy.inspection)|Where-Object {$_ -match '^package:.*/base\.apk$'}|Select-Object -First 1
    if(!$remote){throw '未读取到设备中文应用；请连接一台已授权的 Quest。'}
    $remote=$remote.Substring(8).Trim()
    if($remote -notmatch '^/data/app/[A-Za-z0-9_/~=.+-]+/base\.apk$'){throw '设备返回了非预期 APK 路径。'}
    $hashLine=(& $Adb shell sha256sum $remote)|Select-Object -First 1
    if($hashLine -notmatch '^([a-fA-F0-9]{64})\s'){throw '无法读取设备 APK 哈希。'}
    $deviceHash=$Matches[1]
    $InstalledApk=Join-Path $root 'artifacts/device-diagnosis/installed-inspection.apk'
    if(!(Test-Path -LiteralPath $InstalledApk) -or (Get-FileHash -LiteralPath $InstalledApk -Algorithm SHA256).Hash -ne $deviceHash){
        New-Item -ItemType Directory -Path (Split-Path -Parent $InstalledApk) -Force|Out-Null
        & $Adb pull $remote $InstalledApk|Out-Null
        if($LASTEXITCODE -ne 0){throw '读取设备 APK 失败。'}
    }
    if((Get-FileHash -LiteralPath $InstalledApk -Algorithm SHA256).Hash -ne $deviceHash){throw '设备 APK 与读取副本不一致。'}
    $deviceVerified=$true
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Read-LessonFingerprint([string]$path) {
    $absolute=(Resolve-Path -LiteralPath $path).Path
    $zip=[System.IO.Compression.ZipFile]::OpenRead($absolute)
    try {
        $entry=$zip.Entries|Where-Object {$_.FullName.EndsWith('global-metadata.dat')}|Select-Object -First 1
        if(!$entry){throw "IL2CPP metadata missing: $absolute"}
        $stream=$entry.Open();$memory=[System.IO.MemoryStream]::new()
        try {$stream.CopyTo($memory);$metadata=[System.Text.Encoding]::UTF8.GetString($memory.ToArray())}
        finally {$stream.Dispose();$memory.Dispose()}
        [pscustomobject]@{
            file=$absolute
            sha256=(Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash
            modelTransfer=$metadata.Contains('LayoutModelTask')
            handDrivenDoor=$metadata.Contains('SampleDoorHand')
            panelQuestion=$metadata.Contains('BeginQuestion')
            roomObservation=$metadata.Contains('ShowRoomObservation')
            circleCourse=$metadata.Contains('CircleLesson') -and $metadata.Contains('CircleProgress')
            panoramaViewer=$metadata.Contains('CircleMediaViewer')
        }
    } finally {$zip.Dispose()}
}
$installed=Read-LessonFingerprint $InstalledApk
$candidate=Read-LessonFingerprint $CandidateApk
$report=[pscustomobject]@{
    liveDeviceHashVerified=$deviceVerified
    installed=$installed
    candidate=$candidate
    identical=$installed.sha256 -eq $candidate.sha256
    installedHasHandDrivenDoor=$installed.handDrivenDoor
    candidateHasHandDrivenDoor=$candidate.handDrivenDoor
    installedHasCircleCourse=$installed.circleCourse
    candidateHasCircleCourse=$candidate.circleCourse
}
$json=$report|ConvertTo-Json -Depth 4
if($OutputPath){Set-Content -LiteralPath $OutputPath -Value $json -Encoding utf8}
$json
# Exact content, not a reused visible version number, decides whether installation matches.
if(!$report.identical){exit 2}
