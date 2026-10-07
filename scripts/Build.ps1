[CmdletBinding()]
param(
    [string]$NavisworksInstallDir,
    [string]$MSBuildPath,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PathFinder.Tools.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$navisworksPath = Resolve-NavisworksInstallDir -NavisworksInstallDir $NavisworksInstallDir
$buildTool = Resolve-PathFinderMSBuild -MSBuildPath $MSBuildPath
$solutionPath = Join-Path $repoRoot 'AddinRibbon.sln'

Write-Host "Building PathFinder for Navisworks Manage 2027 from $navisworksPath."
& $buildTool $solutionPath /t:Rebuild "/p:Configuration=$Configuration" '/p:Platform=Any CPU' "/p:NavisworksInstallDir=$navisworksPath" /m /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

$assemblyPath = Join-Path $repoRoot "AddinRibbon\bin\$Configuration\AddinRibbon.dll"
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Build completed but the expected output is missing: $assemblyPath."
}
Write-Host "Built $assemblyPath."
