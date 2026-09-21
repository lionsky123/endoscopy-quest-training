param([switch]$PublishRooms,[switch]$PublishClinical,[switch]$ValidateFullScript)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $taskRoot 'artifacts/full-script-model-import'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Import-Module (Join-Path $taskRoot 'app/Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$context = Enter-BotanicalGardenUnityValidationMirror -UnityExecutable 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ProjectPath (Join-Path $taskRoot 'app')
try {
    $arguments = @('-batchmode','-buildTarget','Android','-projectPath',('"'+$context.MirrorRoot+'"'),
        '-executeMethod',$(if($PublishRooms -or $PublishClinical){'BotanicalGardenQR.Bootstrap.Editor.FullScriptRoomPublish.Publish'}else{'BotanicalGardenQR.Bootstrap.Editor.FullScriptModelImport.Prepare'}),
        '-bgqrCaptureOutput',('"'+$output+'"'),'-logFile',('"'+(Join-Path $output 'prepare.log')+'"'))
    if($PublishClinical){$arguments+='-bgqrClinicalRoom'}
    $process = Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Model preparation failed: $($process.ExitCode); see $output/prepare.log" }
    if($PublishRooms -and $PublishClinical){
        $officeArguments=@($arguments | Where-Object {$_ -ne '-bgqrClinicalRoom'})
        $office=Start-Process -FilePath $context.UnityPath -ArgumentList $officeArguments -PassThru -WindowStyle Hidden
        $office.WaitForExit()
        if($office.ExitCode -ne 0){throw 'Office publication failed.'}
    }
    # Publish only this task's isolated import directory, not the whole mirror or user settings.
    $relative = if($PublishRooms -or $PublishClinical){'Assets/EndoscopyTheme/Resources/FullScriptRooms'}else{'Assets/EndoscopyTheme/ImportedModels'}
    $mirrorAssets = Join-Path $context.MirrorRoot $relative
    $realAssets = Join-Path $taskRoot ('app/' + $relative)
    Copy-Item -LiteralPath ($mirrorAssets + '.meta') -Destination ($realAssets + '.meta') -Force
    Get-ChildItem -LiteralPath $mirrorAssets -File -Recurse | ForEach-Object {
        $suffix = $_.FullName.Substring($mirrorAssets.Length).TrimStart('\','/')
        $target = Join-Path $realAssets $suffix
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
    Write-Output 'Requested model preparation/publication completed. No APK build.'
    if($ValidateFullScript){
        $validationOutput=Join-Path $taskRoot 'artifacts/full-script-integrated'
        New-Item -ItemType Directory -Path $validationOutput -Force | Out-Null
        $renderArguments=@('-batchmode','-buildTarget','Android','-projectPath',('"'+$context.MirrorRoot+'"'),'-executeMethod','BotanicalGardenQR.Bootstrap.Editor.FullScriptJourneyPreview.Capture','-bgqrCaptureOutput',('"'+$validationOutput+'"'),'-logFile',('"'+(Join-Path $validationOutput 'render.log')+'"'))
        $render=Start-Process -FilePath $context.UnityPath -ArgumentList $renderArguments -PassThru -WindowStyle Hidden
        $render.WaitForExit()
        if($render.ExitCode -ne 0){throw 'Full-script editor preview failed.'}
        $results=Join-Path $validationOutput 'tests.xml'
        $testArguments=@('-batchmode','-nographics','-buildTarget','Android','-projectPath',('"'+$context.MirrorRoot+'"'),'-runTests','-testPlatform','EditMode','-testFilter','"FullScriptRuntimeTests;ClinicalJourneySessionTests;ClinicalTrainingRecordsTests;SilentVisitorAudioTests;VirtualRoomGuidePathTests"','-testResults',('"'+$results+'"'),'-logFile',('"'+(Join-Path $validationOutput 'tests.log')+'"'))
        $testStarted=Get-Date
        $test=Start-Process -FilePath $context.UnityPath -ArgumentList $testArguments -PassThru -WindowStyle Hidden
        $test.WaitForExit()
        if(!(Test-Path -LiteralPath $results)){throw 'Fresh full-script test report missing.'}
        if((Get-Item -LiteralPath $results).LastWriteTime -lt $testStarted){throw 'Full-script test report is stale.'}
        [xml]$report=Get-Content -LiteralPath $results -Raw
        $report.'test-run' | Select-Object result,total,passed,failed | Format-List
        $report.SelectNodes('//test-case[failure]') | ForEach-Object {$_.fullname;$_.failure.InnerText}
        if($test.ExitCode -ne 0 -or $report.'test-run'.result -ne 'Passed' -or [int]$report.'test-run'.total -le 0){throw 'Full-script regression failed.'}
    }
}
finally { Exit-BotanicalGardenUnityValidationMirror -Context $context }
