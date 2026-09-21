param([switch]$Preview,[switch]$Validate)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$output=Join-Path $taskRoot 'artifacts/lobby-gaussian-import'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Import-Module (Join-Path $taskRoot 'app/Tools/BotanicalGardenUnityValidationCache.psm1') -Force
$context=Enter-BotanicalGardenUnityValidationMirror -UnityExecutable 'D:/unityhub/unity22.3.62f3c1/6000.3.23f1/Editor/Unity.exe' -ProjectPath (Join-Path $taskRoot 'app')
try {
    $arguments=@('-batchmode','-buildTarget','Android','-projectPath',('"'+$context.MirrorRoot+'"'),
        '-executeMethod',$(if($Preview){'BotanicalGardenQR.Bootstrap.Editor.FullScriptGaussianImport.Preview'}else{'BotanicalGardenQR.Bootstrap.Editor.FullScriptGaussianImport.Prepare'}),
        '-bgqrLobbySource',('"'+(Join-Path $taskRoot 'app/_IncomingModels/Lobby/b660f052d2670589c7476bc95309ed56.ply')+'"'),
        '-bgqrCaptureOutput',('"'+$output+'"'),'-logFile',('"'+(Join-Path $output 'import.log')+'"'))
    if($Preview){$arguments+='-force-vulkan'}
    Write-Output 'Android editor asset conversion/preview only; no APK build or runtime scene changes.'
    $started=Get-Date
    $process=Start-Process -FilePath $context.UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw "Gaussian import failed. See $output/import.log"}
    if($Validate){
        $testReport=Join-Path $output 'tests.xml'
        $testStarted=Get-Date
        $testArguments=@('-batchmode','-nographics','-buildTarget','Android','-projectPath',('"'+$context.MirrorRoot+'"'),
            '-runTests','-testPlatform','EditMode','-testFilter','"FullScriptRuntimeTests;ClinicalJourneySessionTests;ClinicalTrainingRecordsTests;ClinicalCourseTests;ClinicalObservationInteractionTests;SilentVisitorAudioTests"',
            '-testResults',('"'+$testReport+'"'),'-logFile',('"'+(Join-Path $output 'tests.log')+'"'))
        $tests=Start-Process -FilePath $context.UnityPath -ArgumentList $testArguments -PassThru -WindowStyle Hidden
        $tests.WaitForExit()
        if(!(Test-Path -LiteralPath $testReport) -or (Get-Item -LiteralPath $testReport).LastWriteTime -lt $testStarted){throw 'Fresh regression report missing.'}
        [xml]$report=Get-Content -LiteralPath $testReport -Raw
        $report.'test-run' | Select-Object result,total,passed,failed,duration | Format-List
        $report.SelectNodes('//test-case[failure]') | ForEach-Object {$_.fullname;$_.failure.InnerText}
        if($tests.ExitCode -ne 0 -or $report.'test-run'.result -ne 'Passed'){throw 'Post-import regression failed.'}
    }
    if($Preview){Write-Output "Preview images: $output";return}
    $report=Join-Path $output 'import-report.json'
    if(!(Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTime -lt $started){throw 'Fresh import report missing.'}
    $relative='Assets/EndoscopyTheme/ImportedModels/LobbyGaussian'
    $sourceAssets=Join-Path $context.MirrorRoot $relative
    $targetAssets=Join-Path $taskRoot ('app/'+$relative)
    foreach($file in Get-ChildItem -LiteralPath $sourceAssets -File -Recurse){
        $suffix=$file.FullName.Substring($sourceAssets.Length).TrimStart('\','/')
        $target=Join-Path $targetAssets $suffix
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    Copy-Item -LiteralPath ($sourceAssets+'.meta') -Destination ($targetAssets+'.meta') -Force
    Copy-Item -LiteralPath (Join-Path $context.MirrorRoot 'Packages/packages-lock.json') -Destination (Join-Path $taskRoot 'app/Packages/packages-lock.json') -Force
    Get-Content -LiteralPath $report
}
finally {Exit-BotanicalGardenUnityValidationMirror -Context $context}
