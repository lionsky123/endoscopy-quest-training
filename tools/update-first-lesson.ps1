param([string]$UnityExecutable = 'D:\unityhub\unity22.3.62f3c1\6000.3.23f1\Editor\Unity.exe', [switch]$PreviewOnly, [switch]$ProjectionOnly)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../app'))
Import-Module (Join-Path $project 'Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$context = $null
$output = Join-Path ([IO.Path]::GetTempPath()) ('EndoscopyFirstLesson-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$files = @(
    'Assets/BotanicalGardenQR/Content/Authoring/Scenes/giant_saguaro/ContentSceneConfig.asset',
    'Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset',
    'Assets/BotanicalGardenQR/Content/Scenes/giant_saguaro/Endoscopy/cleaning-room-360-v2.png.meta',
    'Assets/BotanicalGardenQR/Content/Scenes/giant_saguaro/Endoscopy/room-perspective-v1.png.meta'
)
$baseline = @{}
foreach ($relative in $files) { $baseline[$relative] = (Get-FileHash -LiteralPath (Join-Path $project $relative)).Hash }
try {
    $context = Enter-BotanicalGardenUnityValidationMirror -UnityExecutable $UnityExecutable -ProjectPath $project
    $env:ENDOSCOPY_FIRST_LESSON_PREVIEW = $output
    Write-Host "Evidence: $output"
    $arguments = @('-batchmode', '-buildTarget', 'Android', '-projectPath', ('"' + $context.MirrorRoot + '"'),
        '-executeMethod', $(if ($ProjectionOnly) { 'EndoscopyTheme.Editor.ClinicalPanoramaProjectionCheck.Capture' } elseif ($PreviewOnly) { 'EndoscopyTheme.Editor.ClinicalFirstLessonPreview.Capture' } else { 'EndoscopyTheme.Editor.ClinicalFirstLessonPreview.UpdateAndCapture' }), '-quit',
        '-logFile', ('"' + (Join-Path $output 'update.log') + '"'))
    $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Editor content update failed. See $output/update.log" }
    foreach ($relative in $(if ($PreviewOnly -or $ProjectionOnly) { @() } else { $files })) {
        if ((Get-FileHash -LiteralPath (Join-Path $project $relative)).Hash -ne $baseline[$relative]) { throw "Source changed: $relative" }
        if (-not (Test-Path -LiteralPath (Join-Path $context.MirrorRoot $relative))) { throw "Missing result: $relative" }
    }
    foreach ($relative in $(if ($PreviewOnly -or $ProjectionOnly) { @() } else { $files })) {
        $backup = Join-Path (Join-Path $output 'source-backup') $relative
        New-Item -ItemType Directory -Path (Split-Path $backup) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $project $relative) -Destination $backup
        Copy-Item -LiteralPath (Join-Path $context.MirrorRoot $relative) -Destination (Join-Path $project $relative)
    }
    $artifacts = [IO.Path]::GetFullPath((Join-Path $project '../artifacts/first-lesson'))
    if ($ProjectionOnly) { Get-Content (Join-Path $output 'projection-check.txt'); return }
    foreach ($image in @('first-lesson-panel.png', 'first-lesson-panorama-controls.png', 'first-lesson-detail-1.png', 'first-lesson-detail-2.png', 'first-lesson-detail-3.png', 'first-lesson-comparison-1.png', 'first-lesson-comparison-2.png', 'first-lesson-comparison-3.png', 'first-lesson-immersive.png', 'first-lesson-guided-card.png', 'projection-check.txt', 'panorama-view-0.png', 'panorama-view-1.png', 'panorama-view-2.png', 'panorama-view-3.png', 'panorama-eye-left.png', 'panorama-eye-right.png')) {
        Copy-Item -LiteralPath (Join-Path $output $image) -Destination (Join-Path $artifacts $image)
    }
    Write-Host 'First lesson content and previews updated. No APK build.'
}
finally {
    Remove-Item Env:ENDOSCOPY_FIRST_LESSON_PREVIEW -ErrorAction SilentlyContinue
    if ($null -ne $context) { Exit-BotanicalGardenUnityValidationMirror -Context $context }
}
