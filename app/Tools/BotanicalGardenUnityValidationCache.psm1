Set-StrictMode -Version Latest

# Restricted hosts can run with a PATH that omits the Windows system directories.
# Unity's batch pipeline (Bee, IL post-processing runner, shader compiler) then
# fails to spawn its child processes and hangs in "Connectivity with IL Post
# Processor runner" retries. Restore the standard directories when missing so
# every validation entry point inherits a usable process environment.
$__bgqrSystem = [Environment]::GetFolderPath([Environment+SpecialFolder]::System)
$__bgqrWindows = Split-Path -Parent $__bgqrSystem
$__bgqrStandardPaths = @(
    $__bgqrSystem,
    (Join-Path $__bgqrWindows 'System32\Wbem'),
    (Join-Path $__bgqrWindows 'WindowsPowerShell\v1.0'),
    $__bgqrWindows
)
$__bgqrExistingPath = @($env:Path -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$__bgqrMissingPaths = @($__bgqrStandardPaths | Where-Object { $__bgqrExistingPath -notcontains $_ })
if ($__bgqrMissingPaths.Count -ne 0) {
    $env:Path = ($__bgqrMissingPaths + $__bgqrExistingPath) -join ';'
}

# Unity Hub and Unity.Licensing.Client are intentionally non-blocking. Only
# processes that can own or mutate a Unity project/validation job belong here.
$script:UnityRelatedProcessNames = @(
    'Unity',
    'UnityShaderCompiler',
    'UnityPackageManager',
    'UnityCrashHandler64',
    'AssetImportWorker',
    'bee',
    'bee_backend'
)

function Get-BotanicalGardenUnityRelatedProcesses {
    @(Get-Process -Name $script:UnityRelatedProcessNames -ErrorAction SilentlyContinue)
}

function Wait-BotanicalGardenUnityRelatedProcesses {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 60)]
        [int] $TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $processes = @(Get-BotanicalGardenUnityRelatedProcesses)
        if ($processes.Count -eq 0) {
            return @()
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    @(Get-BotanicalGardenUnityRelatedProcesses)
}

function Assert-BotanicalGardenPathUnderRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must stay under $fullRoot but resolved to $fullPath"
    }

    $fullPath
}

function Format-BotanicalGardenProcessSummary {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process[]] $Processes
    )

    ($Processes | ForEach-Object { "$($_.ProcessName) ($($_.Id))" }) -join ', '
}

function Remove-BotanicalGardenValidationDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $safePath = Assert-BotanicalGardenPathUnderRoot `
        -Path $Path `
        -Root $Root `
        -Label $Label
    if (-not [System.IO.Directory]::Exists($safePath)) {
        return
    }

    # Windows PowerShell's Remove-Item -Recurse can fail partway through Meta's
    # deeply nested PackageCache even when the validation mirror root is short.
    # The extended-length prefix keeps deletion inside the already validated
    # mirror root while allowing the generated Library to be removed atomically.
    $extendedPath = if ($safePath.StartsWith('\\')) {
        '\\?\UNC\' + $safePath.TrimStart('\')
    }
    else {
        '\\?\' + $safePath
    }
    [System.IO.Directory]::Delete($extendedPath, $true)
    if ([System.IO.Directory]::Exists($safePath)) {
        throw "$Label still exists after deletion: $safePath"
    }
}

function Sync-BotanicalGardenValidationDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Destination,

        [Parameter(Mandatory = $true)]
        [string] $ValidationRoot,

        [Parameter(Mandatory = $true)]
        [string] $RobocopyExecutable,

        [Parameter()]
        [string[]] $ExcludedFileNames = @()
    )

    $safeDestination = Assert-BotanicalGardenPathUnderRoot `
        -Path $Destination `
        -Root $ValidationRoot `
        -Label 'Validation mirror destination'
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        if (Test-Path -LiteralPath $safeDestination) {
            Remove-BotanicalGardenValidationDirectory `
                -Path $safeDestination `
                -Root $ValidationRoot `
                -Label 'Unused validation mirror destination'
        }
        return
    }

    New-Item -ItemType Directory -Path $safeDestination -Force | Out-Null
    $copyArguments = @(
        ('"' + $Source + '"'),
        ('"' + $safeDestination + '"'),
        '/MIR',
        '/COPY:DAT',
        '/DCOPY:DAT',
        '/R:2',
        '/W:1',
        '/XJ',
        '/NFL',
        '/NDL',
        '/NJH',
        '/NJS',
        '/NP'
    )
    # Windows PowerShell 5.1 can unwrap an empty array argument to null under
    # StrictMode; guard it explicitly before inspecting its length.
    if ($null -ne $ExcludedFileNames -and $ExcludedFileNames.Length -gt 0) {
        $copyArguments += '/XF'
        $copyArguments += @($ExcludedFileNames | ForEach-Object { '"' + $_ + '"' })
    }
    $copyProcess = Start-Process `
        -FilePath $RobocopyExecutable `
        -ArgumentList $copyArguments `
        -PassThru `
        -Wait `
        -WindowStyle Hidden
    if ($copyProcess.ExitCode -ge 8) {
        throw "Failed to synchronize $Source to the validation mirror. Robocopy exit code: $($copyProcess.ExitCode)"
    }
}

function Enter-BotanicalGardenUnityValidationMirror {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $UnityExecutable,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $ProjectPath,

        [Parameter()]
        [switch] $ResetValidationLibrary
    )

    if (-not (Test-Path -LiteralPath $UnityExecutable -PathType Leaf)) {
        throw "Unity executable is not a file: $UnityExecutable"
    }
    if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
        throw "ProjectPath is not a directory: $ProjectPath"
    }

    $unityPath = (Resolve-Path -LiteralPath $UnityExecutable).Path
    $projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets') -PathType Container) -or
        -not (Test-Path -LiteralPath (Join-Path $projectRoot 'ProjectSettings') -PathType Container)) {
        throw "ProjectPath is not a Unity project: $projectRoot"
    }

    # User-approved 2026-08-19: mirror jobs may run while the user's Unity Editor
    # has the real project open. Parallel mirror use stays serialized by the
    # exclusive cache lock below; these processes are diagnostic context only.
    $runningProcesses = @(Get-BotanicalGardenUnityRelatedProcesses)
    if ($runningProcesses.Count -ne 0) {
        $processSummary = Format-BotanicalGardenProcessSummary -Processes $runningProcesses
        Write-Host "Unity-related processes are running and will not be stopped: $processSummary"
    }

    $projectVersionPath = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
    $projectVersion = if (Test-Path -LiteralPath $projectVersionPath -PathType Leaf) {
        (Get-Content -LiteralPath $projectVersionPath -Raw).Trim()
    }
    else {
        'unknown-editor-version'
    }
    $cacheIdentity = @(
        $projectRoot.ToUpperInvariant(),
        $unityPath.ToUpperInvariant(),
        $projectVersion
    ) -join "`n"
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        $cacheHash = -join ($hasher.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($cacheIdentity)) |
            ForEach-Object { $_.ToString('x2') })
    }
    finally {
        $hasher.Dispose()
    }

    $localApplicationData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if ([string]::IsNullOrWhiteSpace($localApplicationData)) {
        throw 'Local application data path is unavailable; cannot create an isolated validation cache.'
    }

    # Unity's Mono assembly validator still fails on some PackageCache paths
    # above the legacy Windows path limit. Keep this root deliberately short;
    # the project path, Unity executable and editor version still define the key.
    $cacheBase = Join-Path $localApplicationData 'BGQRValidation\v2'
    $cacheKey = $cacheHash.Substring(0, 16)
    $cacheRoot = Assert-BotanicalGardenPathUnderRoot `
        -Path (Join-Path $cacheBase $cacheKey) `
        -Root $cacheBase `
        -Label 'Validation cache'
    $mirrorRoot = Assert-BotanicalGardenPathUnderRoot `
        -Path (Join-Path $cacheRoot 'P') `
        -Root $cacheRoot `
        -Label 'Validation mirror'
    $cacheLockPath = Assert-BotanicalGardenPathUnderRoot `
        -Path (Join-Path $cacheBase ($cacheKey + '.lock')) `
        -Root $cacheBase `
        -Label 'Validation cache lock'
    $robocopyCommand = Get-Command robocopy.exe -ErrorAction SilentlyContinue
    $robocopyExecutable = if ($null -ne $robocopyCommand) {
        $robocopyCommand.Source
    }
    else {
        # Some restricted host environments omit System32 from PATH.
        Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'robocopy.exe'
    }
    if (-not (Test-Path -LiteralPath $robocopyExecutable -PathType Leaf)) {
        throw "robocopy.exe is unavailable via PATH or the system folder."
    }
    $cacheLock = $null

    try {
        New-Item -ItemType Directory -Path $cacheBase -Force | Out-Null
        try {
            $cacheLock = [System.IO.File]::Open(
                $cacheLockPath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
        }
        catch [System.IO.IOException] {
            throw "Another Unity validation is already using cache $cacheRoot. Wait for it to finish; parallel use of one Unity Library is forbidden."
        }

        $runningProcesses = @(Get-BotanicalGardenUnityRelatedProcesses)
        if ($runningProcesses.Count -ne 0) {
            $processSummary = Format-BotanicalGardenProcessSummary -Processes $runningProcesses
            Write-Host "Unity-related processes present while acquiring the validation mirror (allowed): $processSummary"
        }

        if ($ResetValidationLibrary) {
            $libraryRoot = Join-Path $mirrorRoot 'Library'
            if (Test-Path -LiteralPath $libraryRoot -PathType Container) {
                $safeLibraryRoot = Assert-BotanicalGardenPathUnderRoot `
                    -Path $libraryRoot `
                    -Root $mirrorRoot `
                    -Label 'Validation Library reset target'
                Remove-BotanicalGardenValidationDirectory `
                    -Path $safeLibraryRoot `
                    -Root $mirrorRoot `
                    -Label 'Validation Library reset target'
                Write-Host "Reset generated validation Library: $safeLibraryRoot"
            }
        }

        $cacheWasWarm = Test-Path -LiteralPath (Join-Path $mirrorRoot 'Library') -PathType Container
        New-Item -ItemType Directory -Path $mirrorRoot -Force | Out-Null
        $syncTimer = [System.Diagnostics.Stopwatch]::StartNew()
        foreach ($folder in @('Assets', 'Packages', 'ProjectSettings', 'docs', 'Tools')) {
            $excludedFileNames = if ($folder -eq 'Assets') {
                @('DevAgentSettings.asset', 'DevAgentSettings.asset.meta')
            }
            elseif ($folder -eq 'Tools') {
                @('VolcengineTts.local.key')
            }
            else {
                @()
            }
            Sync-BotanicalGardenValidationDirectory `
                -Source (Join-Path $projectRoot $folder) `
                -Destination (Join-Path $mirrorRoot $folder) `
                -ValidationRoot $mirrorRoot `
                -RobocopyExecutable $robocopyExecutable `
                -ExcludedFileNames $excludedFileNames
        }
        foreach ($credentialRelativePath in @(
            'Assets\Resources\DevAgentSettings.asset',
            'Assets\Resources\DevAgentSettings.asset.meta')) {
            $credentialMirrorPath = Assert-BotanicalGardenPathUnderRoot `
                -Path (Join-Path $mirrorRoot $credentialRelativePath) `
                -Root $mirrorRoot `
                -Label 'Excluded validation credential artifact'
            if (Test-Path -LiteralPath $credentialMirrorPath -PathType Leaf) {
                Remove-Item -LiteralPath $credentialMirrorPath -Force
            }
        }
        $syncTimer.Stop()

        [pscustomobject]@{
            UnityPath = $unityPath
            ProjectRoot = $projectRoot
            CacheRoot = $cacheRoot
            MirrorRoot = $mirrorRoot
            CacheWasWarm = $cacheWasWarm
            SyncSeconds = $syncTimer.Elapsed.TotalSeconds
            LockStream = $cacheLock
        }
    }
    catch {
        if ($null -ne $cacheLock) {
            $cacheLock.Dispose()
        }
        throw
    }
}

function Exit-BotanicalGardenUnityValidationMirror {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNull()]
        [object] $Context
    )

    try {
        $credentialArtifactFound = $false
        foreach ($credentialRelativePath in @(
            'Assets\Resources\DevAgentSettings.asset',
            'Assets\Resources\DevAgentSettings.asset.meta')) {
            $credentialMirrorPath = Assert-BotanicalGardenPathUnderRoot `
                -Path (Join-Path $Context.MirrorRoot $credentialRelativePath) `
                -Root $Context.MirrorRoot `
                -Label 'Generated validation credential artifact'
            if (Test-Path -LiteralPath $credentialMirrorPath -PathType Leaf) {
                $credentialArtifactFound = $true
                Remove-Item -LiteralPath $credentialMirrorPath -Force
            }
        }
        if ($credentialArtifactFound) {
            $libraryRoot = Join-Path $Context.MirrorRoot 'Library'
            if (Test-Path -LiteralPath $libraryRoot -PathType Container) {
                Remove-BotanicalGardenValidationDirectory `
                    -Path $libraryRoot `
                    -Root $Context.MirrorRoot `
                    -Label 'Credential-contaminated validation Library'
                Write-Host "Removed generated credential material and its validation-only Library cache."
            }
        }
    }
    finally {
        if ($null -ne $Context.LockStream) {
            $Context.LockStream.Dispose()
            $Context.LockStream = $null
        }
    }
}

Export-ModuleMember -Function @(
    'Get-BotanicalGardenUnityRelatedProcesses',
    'Wait-BotanicalGardenUnityRelatedProcesses',
    'Enter-BotanicalGardenUnityValidationMirror',
    'Exit-BotanicalGardenUnityValidationMirror'
)
