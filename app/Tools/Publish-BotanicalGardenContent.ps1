[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $UnityExecutable,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ProjectPath,

    [switch] $FieldbookPresentation,
    [switch] $CapturePresentation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$cacheModulePath = Join-Path $PSScriptRoot 'BotanicalGardenUnityValidationCache.psm1'
Import-Module -Name $cacheModulePath -Force

$context = $null
try {
    $context = Enter-BotanicalGardenUnityValidationMirror `
        -UnityExecutable $UnityExecutable `
        -ProjectPath $ProjectPath

    Write-Host ("Publish mirror synchronized in {0:n1}s. Library cache: {1}." -f
        $context.SyncSeconds,
        $(if ($context.CacheWasWarm) { 'warm' } else { 'cold (first run)' }))
    Write-Host "Validation cache: $($context.CacheRoot)"

    if ($FieldbookPresentation) {
        $fieldbookOutput = Join-Path ([IO.Path]::GetTempPath()) ('BGQR-Fieldbook-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $fieldbookOutput -Force | Out-Null
        $fieldbookManifest = Join-Path $fieldbookOutput 'manifest.txt'
        $fieldbookLog = Join-Path $fieldbookOutput 'publish.log'
        $fieldbookRoots = @(
            'Assets/BotanicalGardenQR/Content/Shared/Fieldbook',
            'Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset',
            'Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/CollectionWorldPresentation.prefab',
            'Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/VisitorAtlasHubPresentation.prefab',
            'Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset',
            'Assets/BotanicalGardenQR/Content/Published/PhysicalAugmentationCatalog.asset'
        )
        $fieldbookBaseline = @{}
        foreach ($fieldbookRelative in $fieldbookRoots) {
            $fieldbookTarget = Join-Path $context.ProjectRoot $fieldbookRelative
            $fieldbookMeta = $fieldbookTarget + '.meta'
            if (Test-Path -LiteralPath $fieldbookMeta -PathType Leaf) {
                $fieldbookBaseline[$fieldbookRelative + '.meta'] = (Get-FileHash -LiteralPath $fieldbookMeta).Hash
            }
            if (Test-Path -LiteralPath $fieldbookTarget -PathType Leaf) {
                $fieldbookBaseline[$fieldbookRelative] = (Get-FileHash -LiteralPath $fieldbookTarget).Hash
            } elseif (Test-Path -LiteralPath $fieldbookTarget -PathType Container) {
                Get-ChildItem -LiteralPath $fieldbookTarget -Recurse -File | ForEach-Object {
                    $relative = [IO.Path]::GetRelativePath($context.ProjectRoot, $_.FullName).Replace('\', '/')
                    $fieldbookBaseline[$relative] = (Get-FileHash -LiteralPath $_.FullName).Hash
                }
            }
        }
        $fieldbookArguments = @('-batchmode', '-buildTarget', 'Android', '-nographics', '-quit', '-projectPath', ('"' + $context.MirrorRoot + '"'),
            '-executeMethod', 'BotanicalGardenQR.Bootstrap.Editor.FieldbookAssetPublisher.Run',
            '-fieldbookManifest', ('"' + $fieldbookManifest + '"'), '-logFile', ('"' + $fieldbookLog + '"'))
        if ($CapturePresentation) {
            $fieldbookArguments = @($fieldbookArguments | Where-Object { $_ -ne '-nographics' })
            $fieldbookArguments += @('-bgqrCaptureOutput', ('"' + (Join-Path $fieldbookOutput 'previews') + '"'))
        }
        $fieldbookProcess = Start-Process -FilePath $context.UnityPath -ArgumentList $fieldbookArguments -PassThru -WindowStyle Hidden
        if (-not $fieldbookProcess.WaitForExit(900000)) { throw "Fieldbook publisher still running; process was not stopped. PID=$($fieldbookProcess.Id)" }
        if ($fieldbookProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $fieldbookManifest)) {
            throw "Fieldbook publisher failed. See $fieldbookLog"
        }
        if (-not (Select-String -LiteralPath $fieldbookLog -SimpleMatch 'FIELD BOOK PUBLISH PASSED' -Quiet)) {
            throw "Missing fieldbook publisher success marker. See $fieldbookLog"
        }
        $fieldbookFiles = @(Get-Content -LiteralPath $fieldbookManifest)
        foreach ($relative in $fieldbookFiles) {
            $allowed = $relative.StartsWith($fieldbookRoots[0] + '/', [StringComparison]::Ordinal) -or
                $relative -eq ($fieldbookRoots[0] + '.meta') -or $relative -eq $fieldbookRoots[1] -or
                $relative -eq ($fieldbookRoots[1] + '.meta') -or $relative -eq $fieldbookRoots[2] -or $relative -eq ($fieldbookRoots[2] + '.meta') -or $relative -eq $fieldbookRoots[3] -or $relative -eq ($fieldbookRoots[3] + '.meta') -or $relative -eq $fieldbookRoots[4] -or $relative -eq ($fieldbookRoots[4] + '.meta') -or $relative -eq $fieldbookRoots[5] -or $relative -eq ($fieldbookRoots[5] + '.meta')
            $target = [IO.Path]::GetFullPath((Join-Path $context.ProjectRoot $relative))
            if (-not $allowed -or -not $target.StartsWith($context.ProjectRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
                throw "Invalid fieldbook output path: $relative"
            }
            if (-not (Test-Path -LiteralPath (Join-Path $context.MirrorRoot $relative) -PathType Leaf)) { throw "Missing output: $relative" }
            if ($fieldbookBaseline.ContainsKey($relative)) {
                if (-not (Test-Path -LiteralPath $target -PathType Leaf) -or
                    (Get-FileHash -LiteralPath $target).Hash -ne $fieldbookBaseline[$relative]) {
                    throw "Source changed during publishing: $relative"
                }
                if ($relative.EndsWith('.meta')) {
                    $beforeGuid = [regex]::Match((Get-Content -LiteralPath $target -Raw), '(?m)^guid: ([0-9a-f]{32})').Value
                    $afterGuid = [regex]::Match((Get-Content -LiteralPath (Join-Path $context.MirrorRoot $relative) -Raw), '(?m)^guid: ([0-9a-f]{32})').Value
                    if (-not $beforeGuid -or $beforeGuid -ne $afterGuid) { throw "Publisher changed an existing GUID: $relative" }
                }
            } elseif (Test-Path -LiteralPath $target) {
                throw "A new source file appeared during publishing: $relative"
            }
        }
        # Keep a complete rollback copy outside the repository before replacing any output.
        $fieldbookWritten = [System.Collections.Generic.List[string]]::new()
        $fieldbookBackup = Join-Path $fieldbookOutput 'source-backup'
        foreach ($relative in $fieldbookFiles) {
            if ($fieldbookBaseline.ContainsKey($relative)) {
                $backup = Join-Path $fieldbookBackup $relative
                New-Item -ItemType Directory -Path (Split-Path $backup) -Force | Out-Null
                Copy-Item -LiteralPath (Join-Path $context.ProjectRoot $relative) -Destination $backup
            }
        }
        try {
            foreach ($relative in $fieldbookFiles) {
                $source = Join-Path $context.MirrorRoot $relative
                $target = Join-Path $context.ProjectRoot $relative
                New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
                $fieldbookWritten.Add($relative)
                Copy-Item -LiteralPath $source -Destination $target -Force
                if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                    throw "Published file hash mismatch: $relative"
                }
            }
        } catch {
            foreach ($relative in $fieldbookWritten) {
                $target = Join-Path $context.ProjectRoot $relative
                if ($fieldbookBaseline.ContainsKey($relative)) {
                    Copy-Item -LiteralPath (Join-Path $fieldbookBackup $relative) -Destination $target -Force
                } elseif (Test-Path -LiteralPath $target -PathType Leaf) {
                    Remove-Item -LiteralPath $target
                }
            }
            throw
        }
        Write-Host "Fieldbook presentation published. Evidence: $fieldbookOutput"
        return
    }

    $relativeLibraryPath = 'Assets\BotanicalGardenQR\Content\Published\ContentSceneLibrary.asset'
    $mirrorLibraryPath = Join-Path $context.MirrorRoot $relativeLibraryPath
    $projectLibraryPath = Join-Path $context.ProjectRoot $relativeLibraryPath
    $relativePhysicalCatalogPath = 'Assets\BotanicalGardenQR\Content\Published\PhysicalAugmentationCatalog.asset'
    $mirrorPhysicalCatalogPath = Join-Path $context.MirrorRoot $relativePhysicalCatalogPath
    $projectPhysicalCatalogPath = Join-Path $context.ProjectRoot $relativePhysicalCatalogPath
    $mirrorLogPath = Join-Path $context.MirrorRoot 'Logs\content-scene-publish.log'
    $projectLogPath = Join-Path $context.ProjectRoot 'Temp\content-scene-publish.log'
    New-Item -ItemType Directory -Path (Split-Path -Parent $mirrorLogPath) -Force | Out-Null

    $unityArguments = @(
        '-batchmode', '-buildTarget', 'Android',
        '-nographics',
        '-quit',
        '-projectPath',
        ('"' + $context.MirrorRoot + '"'),
        '-executeMethod',
        'BotanicalGardenQR.Configuration.Editor.ContentScenePublishCli.Run',
        '-logFile',
        ('"' + $mirrorLogPath + '"')
    )
    $unityProcess = Start-Process `
        -FilePath $context.UnityPath `
        -ArgumentList $unityArguments `
        -PassThru `
        -WindowStyle Hidden
    if (-not $unityProcess.WaitForExit(600000)) {
        throw "Content publish Unity process did not exit within 600 seconds and was not stopped. PID=$($unityProcess.Id)"
    }
    $exitCode = $unityProcess.ExitCode

    $remainingProcesses = @(Get-BotanicalGardenUnityRelatedProcesses)
    if ($remainingProcesses.Count -ne 0) {
        $processSummary = ($remainingProcesses | ForEach-Object { "$($_.ProcessName) ($($_.Id))" }) -join ', '
        Write-Host "Unity-related processes still running and not stopped: $processSummary"
    }

    if (Test-Path -LiteralPath $mirrorLogPath -PathType Leaf) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $projectLogPath) -Force | Out-Null
        Copy-Item -LiteralPath $mirrorLogPath -Destination $projectLogPath -Force
    }
    if ($exitCode -ne 0) {
        if (Test-Path -LiteralPath $projectLogPath -PathType Leaf) {
            Get-Content -LiteralPath $projectLogPath -Tail 120
        }
        throw "Content publish failed with Unity exit code $exitCode. See $projectLogPath"
    }
    if (-not (Test-Path -LiteralPath $mirrorLibraryPath -PathType Leaf)) {
        throw "Content publish produced no library at $mirrorLibraryPath"
    }
    if (-not (Test-Path -LiteralPath $mirrorPhysicalCatalogPath -PathType Leaf)) {
        throw "Content publish produced no physical augmentation catalog at $mirrorPhysicalCatalogPath"
    }
    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($context.ProjectRoot).TrimEnd('\') + '\'
    $publishedAssets = @(
        @{
            Source = $mirrorLibraryPath
            SourceMeta = $mirrorLibraryPath + '.meta'
            Target = $projectLibraryPath
            TargetMeta = $projectLibraryPath + '.meta'
            Label = 'content library'
        },
        @{
            Source = $mirrorPhysicalCatalogPath
            SourceMeta = $mirrorPhysicalCatalogPath + '.meta'
            Target = $projectPhysicalCatalogPath
            TargetMeta = $projectPhysicalCatalogPath + '.meta'
            Label = 'physical augmentation catalog'
        }
    )
    foreach ($publishedAsset in $publishedAssets) {
        $resolvedTarget = [System.IO.Path]::GetFullPath($publishedAsset.Target)
        if (-not $resolvedTarget.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to publish outside the project root: $resolvedTarget"
        }
        foreach ($requiredPath in @(
            $publishedAsset.Source,
            $publishedAsset.SourceMeta,
            $resolvedTarget,
            $publishedAsset.TargetMeta)) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Routine content publishing requires an existing asset/meta pair; missing: $requiredPath"
            }
        }
        $sourceGuidMatch = [regex]::Match(
            (Get-Content -LiteralPath $publishedAsset.SourceMeta -Raw),
            '(?m)^guid:\s*([0-9a-f]{32})\s*$')
        $targetGuidMatch = [regex]::Match(
            (Get-Content -LiteralPath $publishedAsset.TargetMeta -Raw),
            '(?m)^guid:\s*([0-9a-f]{32})\s*$')
        if (-not $sourceGuidMatch.Success -or -not $targetGuidMatch.Success -or
            -not [string]::Equals(
                $sourceGuidMatch.Groups[1].Value,
                $targetGuidMatch.Groups[1].Value,
                [System.StringComparison]::Ordinal)) {
            throw "Routine content publishing refuses to change the GUID for $($publishedAsset.Label)."
        }

        $mirrorHash = (Get-FileHash -LiteralPath $publishedAsset.Source -Algorithm SHA256).Hash
        $projectHash = (Get-FileHash -LiteralPath $resolvedTarget -Algorithm SHA256).Hash
        $publishedAsset['ResolvedTarget'] = $resolvedTarget
        $publishedAsset['CopyRequired'] = -not [string]::Equals(
            $mirrorHash,
            $projectHash,
            [System.StringComparison]::OrdinalIgnoreCase)
    }

    $transactionId = [guid]::NewGuid().ToString('N')
    $changedAssets = @($publishedAssets | Where-Object { $_.CopyRequired })
    $retainBackups = $false
    try {
        foreach ($publishedAsset in $changedAssets) {
            $publishedAsset['Stage'] = $publishedAsset.ResolvedTarget + ".$transactionId.stage"
            $publishedAsset['Backup'] = $publishedAsset.ResolvedTarget + ".$transactionId.backup"
            Copy-Item -LiteralPath $publishedAsset.Source -Destination $publishedAsset.Stage
            Copy-Item -LiteralPath $publishedAsset.ResolvedTarget -Destination $publishedAsset.Backup
            $stageHash = (Get-FileHash -LiteralPath $publishedAsset.Stage -Algorithm SHA256).Hash
            $sourceHash = (Get-FileHash -LiteralPath $publishedAsset.Source -Algorithm SHA256).Hash
            if (-not [string]::Equals($stageHash, $sourceHash, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Staged $($publishedAsset.Label) does not match its mirror source."
            }
        }
        foreach ($publishedAsset in $changedAssets) {
            Copy-Item -LiteralPath $publishedAsset.Stage -Destination $publishedAsset.ResolvedTarget -Force
        }
        foreach ($publishedAsset in $publishedAssets) {
            if ($publishedAsset.CopyRequired) {
                Write-Host "Published generated $($publishedAsset.Label): $($publishedAsset.ResolvedTarget)"
            }
            else {
                Write-Host "Generated $($publishedAsset.Label) is already up to date; the project asset was not rewritten."
            }
        }
    }
    catch {
        $publishFailure = $_
        $rollbackFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($publishedAsset in $changedAssets) {
            if ($publishedAsset.ContainsKey('Backup') -and
                (Test-Path -LiteralPath $publishedAsset.Backup -PathType Leaf)) {
                try {
                    Copy-Item -LiteralPath $publishedAsset.Backup -Destination $publishedAsset.ResolvedTarget -Force
                }
                catch {
                    $rollbackFailures.Add(
                        "$($publishedAsset.Label): $($_.Exception.Message); backup=$($publishedAsset.Backup)")
                }
            }
        }
        if ($rollbackFailures.Count -gt 0) {
            $retainBackups = $true
            throw (
                "Content publish failed and rollback was incomplete: $($publishFailure.Exception.Message). " +
                ($rollbackFailures -join ' | '))
        }
        throw $publishFailure
    }
    finally {
        foreach ($publishedAsset in $changedAssets) {
            foreach ($temporaryKey in @('Stage', 'Backup')) {
                if ($publishedAsset.ContainsKey($temporaryKey) -and
                    ($temporaryKey -ne 'Backup' -or -not $retainBackups) -and
                    (Test-Path -LiteralPath $publishedAsset[$temporaryKey] -PathType Leaf)) {
                    Remove-Item -LiteralPath $publishedAsset[$temporaryKey] -Force
                }
            }
        }
    }

}
finally {
    if ($null -ne $context) {
        Exit-BotanicalGardenUnityValidationMirror -Context $context
    }
}
