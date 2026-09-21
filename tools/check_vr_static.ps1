param([string]$UnityEditor = 'D:\unityhub\unity22.3.62f3c1\6000.3.23f1\Editor')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'app'
$out = Join-Path $root 'artifacts/vr-static'
New-Item -ItemType Directory -Path $out -Force | Out-Null
# Roslyn only: does not launch Unity, MSBuild, Gradle, a player build or any editor callbacks.
$sdk = Get-ChildItem 'C:/Program Files/dotnet/sdk' -Directory | Sort-Object Name -Descending | Select-Object -First 1
$pack = Get-ChildItem 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref' -Directory | Sort-Object Name -Descending | Select-Object -First 1
$ref = Get-ChildItem (Join-Path $pack.FullName 'ref') -Directory | Select-Object -First 1
$compiler = Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll'
$engine = @(Get-ChildItem (Join-Path $UnityEditor 'Data/Managed/UnityEngine') -Filter '*.dll')
$framework = @(Get-ChildItem $ref.FullName -Filter '*.dll')
$cached = @(Get-ChildItem (Join-Path $app 'Library/ScriptAssemblies') -Filter '*.dll')
$changed = @{}
$units = @(
    @('BotanicalGardenQR.Experience.Contracts','Assets/BotanicalGardenQR/Experience/Contracts'),
    @('BotanicalGardenQR.Fairy.Contracts','Assets/BotanicalGardenQR/Modules/Fairy/Contracts'),
    @('BotanicalGardenQR.FrontendShell.Contracts','Assets/BotanicalGardenQR/Modules/FrontendShell/Contracts'),
    @('BotanicalGardenQR.Panorama.Contracts','Assets/BotanicalGardenQR/Modules/Panorama/Contracts'),
    @('BotanicalGardenQR.MapNavigation.Contracts','Assets/BotanicalGardenQR/Modules/MapNavigation/Contracts'),
    @('BotanicalGardenQR.MapNavigation.Runtime','Assets/BotanicalGardenQR/Modules/MapNavigation/Runtime'),
    @('BotanicalGardenQR.Configuration.Runtime','Assets/BotanicalGardenQR/Configuration/Runtime'),
    @('BotanicalGardenQR.Experience.Application','Assets/BotanicalGardenQR/Experience/Application'),
    @('BotanicalGardenQR.FrontendShell.Runtime','Assets/BotanicalGardenQR/Modules/FrontendShell/Runtime'),
    @('BotanicalGardenQR.VisitorCoach.Frontend','Assets/BotanicalGardenQR/Modules/VisitorCoach/Frontend'),
    @('BotanicalGardenQR.VisitorPrologue.Frontend','Assets/BotanicalGardenQR/Modules/VisitorPrologue/Frontend'),
    @('BotanicalGardenQR.Panorama.Backend','Assets/BotanicalGardenQR/Modules/Panorama/Backend'),
    @('BotanicalGardenQR.KnowledgeMiniGame.Frontend','Assets/BotanicalGardenQR/Modules/KnowledgeMiniGame/Frontend'),
    @('BotanicalGardenQR.Panorama.Frontend','Assets/BotanicalGardenQR/Modules/Panorama/Frontend'),
    @('BotanicalGardenQR.Fairy.Backend','Assets/BotanicalGardenQR/Modules/Fairy/Backend'),
    @('BotanicalGardenQR.Bootstrap','Assets/BotanicalGardenQR/Experience/Bootstrap'),
    @('BotanicalGardenQR.Configuration.Editor','Assets/BotanicalGardenQR/Configuration/Editor'),
    @('BotanicalGardenQR.Bootstrap.Editor','Assets/BotanicalGardenQR/Experience/Bootstrap/Editor'),
    @('BotanicalGardenQR.Editor.Validation','Assets/BotanicalGardenQR/Editor/Validation'),
    @('EndoscopyTheme.Static','Assets/EndoscopyTheme/Editor')
)
foreach ($unit in $units) {
    $name=$unit[0]; $dir=Join-Path $app $unit[1]
    $sources=@(Get-ChildItem $dir -Filter '*.cs' -Recurse | Where-Object {
        $name -ne 'BotanicalGardenQR.Bootstrap' -or !$_.FullName.StartsWith((Join-Path $dir 'Editor'),[StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object { '"'+$_.FullName+'"' })
    if($name -eq 'BotanicalGardenQR.Configuration.Runtime') {
        $sources+=@(Get-ChildItem (Join-Path $app 'Assets/BotanicalGardenQR/Modules/Activation/Configuration') -Filter '*.cs' | ForEach-Object { '"'+$_.FullName+'"' })
    }
    $references=@($framework+$engine | ForEach-Object { '/reference:"'+$_.FullName+'"' })
    foreach($dll in $cached) {
        if($dll.BaseName -eq $name -or $dll.BaseName -like 'Assembly-CSharp*') { continue }
        $path=if($changed.ContainsKey($dll.BaseName)){$changed[$dll.BaseName]}else{$dll.FullName}
        $references+='/reference:"'+$path+'"'
    }
    $target=Join-Path $out ($name+'.dll')
    $compilerArguments=@('/nologo','/target:library','/nostdlib+','/langversion:9.0','/nowarn:0649,0414','/define:UNITY_EDITOR,UNITY_ANDROID',('/out:"'+$target+'"'))+$references+$sources
    $rsp=Join-Path $out ($name+'.rsp')
    Set-Content -LiteralPath $rsp -Value $compilerArguments -Encoding utf8
    & dotnet $compiler ('@'+$rsp)
    if($LASTEXITCODE -ne 0){throw "Static source check failed: $name"}
    $changed[$name]=$target
    Write-Output "PASS C# source: $name"
}

$sources=@('Assets/BotanicalGardenQR/Modules/MapNavigation/Contracts','Assets/BotanicalGardenQR/Modules/MapNavigation/Runtime') | ForEach-Object { Get-ChildItem (Join-Path $app $_) -Filter '*.cs' | ForEach-Object { '"'+$_.FullName+'"' } }
$checks=Join-Path $out 'VrRouteChecks.dll'
$compilerArguments=@('/nologo','/target:exe','/nostdlib+','/langversion:9.0',('/out:"'+$checks+'"'))+@($framework | ForEach-Object {'/reference:"'+$_.FullName+'"'})+$sources+@('"'+(Join-Path $PSScriptRoot 'VrRouteChecks.cs')+'"')
$rsp=Join-Path $out 'route-checks.rsp'; Set-Content -LiteralPath $rsp -Value $compilerArguments -Encoding utf8
& dotnet $compiler ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'Route checks did not compile.'}
@{runtimeOptions=@{tfm=$ref.Name;framework=@{name='Microsoft.NETCore.App';version=$pack.Name}}} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'VrRouteChecks.runtimeconfig.json')
& dotnet $checks (Join-Path $app 'Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json')
if($LASTEXITCODE -ne 0){throw 'Route domain checks failed.'}
