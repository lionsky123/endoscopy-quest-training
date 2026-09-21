param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $taskRoot 'app/_IncomingModels/Models.zip'
$destination = [IO.Path]::GetFullPath((Join-Path $taskRoot 'app/Assets/EndoscopyTheme/ImportedModels/Source'))
$assets = Join-Path $taskRoot 'app/Assets'
$zip = [IO.Compression.ZipFile]::Open($source, [IO.Compression.ZipArchiveMode]::Read, [Text.Encoding]::GetEncoding(936))
function Get-StreamHash($stream) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}
try {
    # Only data assets; keep existing washing room and Disinfectant untouched.
    $entries = @($zip.Entries | Where-Object {
        $_.Length -gt 0 -and $_.FullName -match '^(Gastroscope/|Gastroscope\.meta$|OfficeInteractions/|OfficeInteractions\.meta$|(computer|table|办公室|诊疗室模型)\.(fbx|FBX)(\.meta)?$)'
    })
    $knownGuids = @{}
    Get-ChildItem -LiteralPath $assets -Filter '*.meta' -Recurse -File | ForEach-Object {
        $match = [regex]::Match([IO.File]::ReadAllText($_.FullName), '(?m)^guid: ([0-9a-f]{32})')
        if ($match.Success) { $knownGuids[$match.Groups[1].Value] = $_.FullName }
    }
    $records = @()
    # Validate every destination, hash, and GUID before extracting anything.
    foreach ($entry in $entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
        if (!$target.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path.' }
        $stream = $entry.Open()
        try { $hash = Get-StreamHash $stream } finally { $stream.Dispose() }
        if (Test-Path -LiteralPath $target) {
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) {
                # Unity legitimately updates import settings in .meta; geometry and material source bytes remain immutable.
                if (!$target.EndsWith('.meta')) { throw "Existing file differs; refusing overwrite: $target" }
            }
        } elseif ($VerifyOnly) { throw "Missing imported source: $target" }
        if ($entry.FullName.EndsWith('.meta')) {
            $reader = [IO.StreamReader]::new($entry.Open())
            try { $match = [regex]::Match($reader.ReadToEnd(), '(?m)^guid: ([0-9a-f]{32})') } finally { $reader.Dispose() }
            if ($match.Success) {
                $guid = $match.Groups[1].Value
                if ($knownGuids.ContainsKey($guid) -and $knownGuids[$guid] -ne $target) { throw "GUID collision: $target and $($knownGuids[$guid])" }
                $knownGuids[$guid] = $target
            }
        }
        $records += [pscustomobject]@{ entry=$entry.FullName; destination=$target; bytes=$entry.Length; sha256=$hash }
    }
    if (!$VerifyOnly) {
        foreach ($record in $records) {
            if (Test-Path -LiteralPath $record.destination) { continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($record.destination)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($record.entry), $record.destination, $false)
        }
        $output = Join-Path $taskRoot 'artifacts/full-script-model-import'
        [IO.Directory]::CreateDirectory($output) | Out-Null
        [IO.File]::WriteAllText((Join-Path $output 'source-manifest.json'), (ConvertTo-Json -InputObject $records -Depth 4), [Text.UTF8Encoding]::new($false))
    }
    Write-Output ("Verified {0} selected assets; source model/material/texture bytes preserved. No APK build." -f $records.Count)
} finally { $zip.Dispose() }
