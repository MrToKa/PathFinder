Set-StrictMode -Version Latest

function Resolve-NavisworksInstallDir {
    param([string]$NavisworksInstallDir)

    $candidates = New-Object 'System.Collections.Generic.List[string]'
    if (-not [string]::IsNullOrWhiteSpace($NavisworksInstallDir)) {
        $candidates.Add($NavisworksInstallDir)
    } else {
        foreach ($registryRoot in @(
            'HKLM:\SOFTWARE\Autodesk\Navisworks Manage\24.0\Location',
            'HKLM:\SOFTWARE\WOW6432Node\Autodesk\Navisworks Manage\24.0\Location',
            'HKCU:\SOFTWARE\Autodesk\Navisworks Manage\24.0\Location'
        )) {
            if (Test-Path -LiteralPath $registryRoot) {
                $location = Get-ItemProperty -LiteralPath $registryRoot -Name Path -ErrorAction SilentlyContinue
                if ($null -ne $location -and -not [string]::IsNullOrWhiteSpace($location.Path)) {
                    $candidates.Add([string]$location.Path)
                }
            }
        }
        $candidates.Add('D:\Programs\Navisworks Manage 2027')
        if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
            $candidates.Add((Join-Path $env:ProgramFiles 'Autodesk\Navisworks Manage 2027'))
        }
    }

    foreach ($candidate in $candidates) {
        $fullPath = [System.IO.Path]::GetFullPath($candidate).TrimEnd('\', '/')
        $apiPath = Join-Path $fullPath 'Autodesk.Navisworks.Api.dll'
        if (-not (Test-Path -LiteralPath $apiPath -PathType Leaf)) { continue }
        if (-not (Test-Path -LiteralPath (Join-Path $fullPath 'AdWindows.dll') -PathType Leaf)) { continue }
        if (-not (Test-Path -LiteralPath (Join-Path $fullPath 'navisworks.gui.roamer.dll') -PathType Leaf)) { continue }
        $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($apiPath)
        if ($version.FileMajorPart -ne 24) {
            throw "Expected Navisworks Manage 2027 API version 24; found $($version.FileVersion) at $fullPath."
        }
        return $fullPath
    }
    throw 'Navisworks Manage 2027 was not found. Pass -NavisworksInstallDir with its installation directory.'
}

function Resolve-PathFinderMSBuild {
    param([string]$MSBuildPath)

    if (-not [string]::IsNullOrWhiteSpace($MSBuildPath)) {
        if (-not (Test-Path -LiteralPath $MSBuildPath -PathType Leaf)) {
            throw "MSBuild was not found at $MSBuildPath."
        }
        return [System.IO.Path]::GetFullPath($MSBuildPath)
    }
    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $vswherePath = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path -LiteralPath $vswherePath -PathType Leaf) {
            $found = @(& $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')
            if ($LASTEXITCODE -eq 0 -and $found.Count -gt 0) {
                foreach ($path in $found) {
                    if (Test-Path -LiteralPath $path -PathType Leaf) { return $path }
                }
            }
        }
    }
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    throw 'Visual Studio MSBuild was not found. Install the .NET desktop build tools or pass -MSBuildPath.'
}

function Assert-ChildPath {
    param([string]$Path, [string]$Parent, [string]$Description)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must be inside $fullParent."
    }
    return $fullPath
}
