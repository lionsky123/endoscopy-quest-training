param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$packagesRoot = Join-Path $ProjectRoot 'app/Packages'
$manifestPath = Join-Path $packagesRoot 'manifest.json'
$lockPath = Join-Path $packagesRoot 'packages-lock.json'
$packagePath = Join-Path $packagesRoot 'org.nesnausk.gaussian-splatting/package.json'

foreach ($path in @($manifestPath, $lockPath, $packagePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required package file is missing: $path"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json

$packageName = 'org.nesnausk.gaussian-splatting'
if ($package.name -ne $packageName) {
    throw "Embedded package name is '$($package.name)', expected '$packageName'."
}

if ($manifest.dependencies.PSObject.Properties.Name -contains $packageName) {
    throw "The Gaussian package must stay embedded under Packages, not re-enter manifest.json."
}

$lockEntry = $lock.dependencies.PSObject.Properties[$packageName]
if ($null -eq $lockEntry) {
    throw "packages-lock.json has no embedded Gaussian package entry."
}

if ($lockEntry.Value.source -ne 'embedded' -or
    $lockEntry.Value.version -ne 'file:org.nesnausk.gaussian-splatting') {
    throw "Gaussian package lock entry is not embedded: source=$($lockEntry.Value.source), version=$($lockEntry.Value.version)"
}

Write-Output "PASS: $packageName is an embedded package and does not require Unity's external git executable."
