[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$NavisworksInstallDir,
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PathFinder.Tools.ps1')
$navisworksPath = Resolve-NavisworksInstallDir -NavisworksInstallDir $NavisworksInstallDir
$pluginsRoot = Join-Path $navisworksPath 'Plugins'
$expectedDestination = [System.IO.Path]::GetFullPath((Join-Path $pluginsRoot 'AddinRibbon')).TrimEnd('\', '/')
if ([string]::IsNullOrWhiteSpace($Destination)) { $Destination = $expectedDestination }
$pluginRoot = [System.IO.Path]::GetFullPath($Destination).TrimEnd('\', '/')
if (-not $pluginRoot.Equals($expectedDestination, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination must be the detected Manage 2027 plugin directory: $expectedDestination."
}
$packageRoot = [System.IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\', '/')
if ($packageRoot.Equals($pluginRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Package source and installed plugin directory must differ.' }
$manifestPath = Join-Path $packageRoot 'package-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'The package manifest is missing. Run Package.ps1 first.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.product -ne 'PathFinder' -or $manifest.navisworks -ne 'Manage 2027' -or $manifest.apiMajorVersion -ne 24) {
    throw 'This package does not target PathFinder for Navisworks Manage 2027.'
}
$installEntries = @($manifest.files | Where-Object {
    $_.path -eq 'AddinRibbon.dll' -or $_.path -eq 'AddinRibbon.xaml' -or $_.path -eq 'en-US/AddinRibbon.xaml' -or $_.path -match '^Images/[A-Za-z0-9_]+\.png$'
})
foreach ($required in @('AddinRibbon.dll', 'AddinRibbon.xaml', 'en-US/AddinRibbon.xaml', 'Images/logo_16x16.png', 'Images/logo_32x32.png')) {
    if (@($installEntries | Where-Object { $_.path -eq $required }).Count -ne 1) { throw "Required package entry is missing or duplicated: $required." }
}
foreach ($entry in $manifest.files) {
    $sourcePath = Assert-ChildPath -Path (Join-Path $packageRoot $entry.path) -Parent $packageRoot -Description 'Manifest source'
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Package file is missing: $($entry.path)." }
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Package checksum mismatch: $($entry.path)." }
}
foreach ($process in @(Get-Process -Name Roamer -ErrorAction SilentlyContinue)) {
    try { $runningPath = $process.Path } catch { $runningPath = $null }
    if ([string]::IsNullOrWhiteSpace($runningPath) -or $runningPath.StartsWith($navisworksPath + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Close Navisworks Manage 2027 before installing or replacing the plugin.'
    }
}
if (-not $PSCmdlet.ShouldProcess($pluginRoot, 'Back up the existing AddinRibbon folder and install the verified package')) { return }

if (Test-Path -LiteralPath $pluginRoot) {
    if (-not (Test-Path -LiteralPath $pluginRoot -PathType Container)) { throw 'The plugin destination is not a directory.' }
    $backupsRoot = Join-Path $navisworksPath 'PathFinderBackups'
    New-Item -ItemType Directory -Path $backupsRoot -Force | Out-Null
    $backupRoot = Join-Path $backupsRoot ('AddinRibbon-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $backupRoot = Assert-ChildPath -Path $backupRoot -Parent $backupsRoot -Description 'Backup directory'
    Copy-Item -LiteralPath $pluginRoot -Destination $backupRoot -Recurse -Force
    Write-Host "Previous plugin backed up at $backupRoot."
}
New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
foreach ($entry in $installEntries) {
    $targetPath = Assert-ChildPath -Path (Join-Path $pluginRoot $entry.path) -Parent $pluginRoot -Description 'Installed plugin file'
    New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $packageRoot $entry.path) -Destination $targetPath -Force
    if ((Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Installed file verification failed: $($entry.path)." }
}
Write-Host "Installed PathFinder at $pluginRoot. Start Navisworks Manage 2027 to load it."
