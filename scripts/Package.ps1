[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [string]$NavisworksInstallDir,
    [string]$MSBuildPath,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PathFinder.Tools.ps1')
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'AddinRibbon'
$buildRoot = Join-Path $sourceRoot 'bin\Release'
$packageRoot = [System.IO.Path]::GetFullPath($Destination)

if (Test-Path -LiteralPath $packageRoot) {
    if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) { throw 'Package destination must be a directory.' }
    if (@(Get-ChildItem -LiteralPath $packageRoot -Force).Count -gt 0) { throw 'Use an empty or new package destination to avoid stale files.' }
}
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -NavisworksInstallDir $NavisworksInstallDir -MSBuildPath $MSBuildPath -Configuration Release
}

$requiredFiles = @(
    @{ Source = (Join-Path $buildRoot 'AddinRibbon.dll'); Relative = 'AddinRibbon.dll' },
    @{ Source = (Join-Path $sourceRoot 'AddinRibbon.xaml'); Relative = 'AddinRibbon.xaml' },
    @{ Source = (Join-Path $sourceRoot 'AddinRibbon.xaml'); Relative = 'en-US\AddinRibbon.xaml' },
    @{ Source = (Join-Path $sourceRoot 'logo_16x16.png'); Relative = 'Images\logo_16x16.png' },
    @{ Source = (Join-Path $sourceRoot 'logo_32x32.png'); Relative = 'Images\logo_32x32.png' }
)
foreach ($entry in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $entry.Source -PathType Leaf)) { throw "Required package file is missing: $($entry.Source)." }
}
$entries = @($requiredFiles)
foreach ($icon in @('1_16.png', '1_32.png')) {
    $iconPath = Join-Path $sourceRoot $icon
    if (Test-Path -LiteralPath $iconPath -PathType Leaf) { $entries += @{ Source = $iconPath; Relative = "Images\$icon" } }
}
foreach ($doc in @('README.md', 'docs\ActionPlan.md', 'docs\Validation.md')) {
    $docPath = Join-Path $repoRoot $doc
    if (Test-Path -LiteralPath $docPath -PathType Leaf) { $entries += @{ Source = $docPath; Relative = $doc } }
}
foreach ($tool in @('Install.ps1', 'PathFinder.Tools.ps1')) {
    $entries += @{ Source = (Join-Path $PSScriptRoot $tool); Relative = "scripts\$tool" }
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$manifestFiles = @()
foreach ($entry in $entries) {
    $targetPath = Assert-ChildPath -Path (Join-Path $packageRoot $entry.Relative) -Parent $packageRoot -Description 'Package file'
    New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
    Copy-Item -LiteralPath $entry.Source -Destination $targetPath -Force
    $manifestFiles += [ordered]@{ path = $entry.Relative.Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifest = [ordered]@{
    product = 'PathFinder'; assembly = 'AddinRibbon.dll'; navisworks = 'Manage 2027'; apiMajorVersion = 24
    createdUtc = [DateTime]::UtcNow.ToString('o'); files = $manifestFiles
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageRoot 'package-manifest.json') -Encoding UTF8
Write-Host "Packaged PathFinder at $packageRoot. Autodesk binaries and source workbooks are excluded."
