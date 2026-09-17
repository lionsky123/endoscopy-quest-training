param([string]$UnityEditor = 'D:\unityhub\unity22.3.62f3c1\6000.3.23f1\Editor')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'app'
$out = Join-Path $root 'artifacts/layout-static'
New-Item -ItemType Directory -Path $out -Force | Out-Null
# Roslyn source checking only. Never starts Unity, MSBuild, Android tooling or an editor callback.
$sdk = Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\sdk' -Directory | Sort-Object Name -Descending | Select-Object -First 1
$referencePack = Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Sort-Object Name -Descending | Select-Object -First 1
$referenceDirectory = Get-ChildItem -LiteralPath (Join-Path $referencePack.FullName 'ref') -Directory | Select-Object -First 1
$compiler = Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll'
$framework = @(Get-ChildItem -LiteralPath $referenceDirectory.FullName -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' })
$engine = @(Get-ChildItem -LiteralPath (Join-Path $UnityEditor 'Data\Managed\UnityEngine') -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' })
$packages = @(Get-ChildItem -LiteralPath (Join-Path $app 'Library\ScriptAssemblies') -Filter '*.dll' | Where-Object { $_.Name -notlike 'Endoscopy.*' -and $_.Name -notlike '*Editor*' -and $_.Name -notlike '*Tests*' } | ForEach-Object { '/reference:"' + $_.FullName + '"' })
$sources = @(Get-ChildItem -LiteralPath (Join-Path $app 'Assets\Endoscopy\Runtime') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' })
$response = @('/nologo','/target:library','/nostdlib+','/langversion:9.0',('/out:"' + (Join-Path $out 'Endoscopy.Runtime.dll') + '"')) + $framework + $engine + $packages + $sources
$responsePath = Join-Path $out 'runtime.rsp'
Set-Content -LiteralPath $responsePath -Value $response -Encoding utf8
& dotnet $compiler ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Runtime C# static compilation failed.' }

$nunit = Get-ChildItem -LiteralPath (Join-Path $app 'Library\PackageCache') -Filter 'nunit.framework.dll' -Recurse | Select-Object -First 1
$editorReferences = @(
    (Join-Path $out 'Endoscopy.Runtime.dll'),
    (Join-Path $app 'Library\ScriptAssemblies\Endoscopy.Editor.dll'),
    (Join-Path $app 'Library\ScriptAssemblies\UnityEditor.TestRunner.dll'),
    $nunit.FullName) | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { '/reference:"' + $_ + '"' }
$regressionResponse = @('/nologo','/target:library','/nostdlib+','/langversion:9.0',('/out:"' + (Join-Path $out 'Endoscopy.Tests.dll') + '"')) + $framework + $engine + $packages + $editorReferences + @(
    ('"' + (Join-Path $app 'Assets\Endoscopy\Tests\Editor\HandsOnTests.cs') + '"'),
    ('"' + (Join-Path $app 'Assets\Endoscopy\Tests\Editor\LayoutInspectionTests.cs') + '"'))
$regressionPath = Join-Path $out 'regression.rsp'
Set-Content -LiteralPath $regressionPath -Value $regressionResponse -Encoding utf8
& dotnet $compiler ('@' + $regressionPath)
if ($LASTEXITCODE -ne 0) { throw 'Updated Unity regression test source does not compile.' }

$testResponse = @('/nologo','/target:exe','/nostdlib+','/langversion:9.0',('/out:"' + (Join-Path $out 'LayoutChecks.dll') + '"')) + $framework + @(
    ('"' + (Join-Path $app 'Assets\Endoscopy\Runtime\LayoutInspection.cs') + '"'),
    ('"' + (Join-Path $PSScriptRoot 'LayoutChecks.cs') + '"'))
$testResponsePath = Join-Path $out 'checks.rsp'
Set-Content -LiteralPath $testResponsePath -Value $testResponse -Encoding utf8
& dotnet $compiler ('@' + $testResponsePath)
if ($LASTEXITCODE -ne 0) { throw 'Layout checks did not compile.' }
$runtime = @{ runtimeOptions = @{ tfm = $referenceDirectory.Name; framework = @{ name = 'Microsoft.NETCore.App'; version = $referencePack.Name } } } | ConvertTo-Json -Depth 5
Set-Content -LiteralPath (Join-Path $out 'LayoutChecks.runtimeconfig.json') -Value $runtime -Encoding utf8
& dotnet (Join-Path $out 'LayoutChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Layout domain checks failed.' }

$guide = Join-Path $app 'Assets\Endoscopy\Resources\Guide\Oppy'
$metas = Get-ChildItem -LiteralPath $guide -Filter '*.meta' -Recurse
$guids = @{}
foreach ($meta in $metas) {
    $match = [regex]::Match((Get-Content -LiteralPath $meta.FullName -Raw),'(?m)^guid: ([a-f0-9]{32})')
    if ($match.Success) { $guids[$match.Groups[1].Value] = $meta.FullName }
}
$missing = @()
foreach ($file in Get-ChildItem -LiteralPath $guide -Recurse -File | Where-Object { $_.Extension -in '.prefab','.controller','.mat','.meta' }) {
    foreach ($match in [regex]::Matches((Get-Content -LiteralPath $file.FullName -Raw),'guid: ([a-f0-9]{32})')) {
        $guid = $match.Groups[1].Value
        if ($guid -notmatch '^0{16}' -and !$guids.ContainsKey($guid)) { $missing += "$($file.Name): $guid" }
    }
}
if ($missing.Count) { throw ('Imported Oppy dependency missing: ' + ($missing -join ', ')) }
$shader = Get-Content -LiteralPath (Join-Path $guide 'Shaders\ToonFoggyOppy.shader') -Raw
if ($shader.Contains('ArrivalBoundary.hlsl')) { throw 'Garden-only shader dependency remains.' }
Write-Output 'PASS: runtime static compilation, layout domain checks, Oppy GUID closure. Unity/Quest not launched.'
