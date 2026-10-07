[CmdletBinding()]
param(
    [string]$NavisworksInstallDir,
    [string]$MSBuildPath,
    [string]$ModelPath,
    [string]$OutputDirectory,
    [ValidateSet('smoke', 'extract-model', 'background-transparency')][string]$Mode = 'smoke',
    [ValidateRange(30, 300)][int]$TimeoutSeconds = 300,
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PathFinder.Tools.ps1')
$repoRoot = Split-Path -Parent $PSScriptRoot
$hostDir = Resolve-NavisworksInstallDir -NavisworksInstallDir $NavisworksInstallDir
$buildTool = Resolve-PathFinderMSBuild -MSBuildPath $MSBuildPath
$testsRoot = Join-Path $repoRoot 'tests\PathFinder.Navisworks.Smoke'
$builtAssembly = Join-Path $repoRoot 'AddinRibbon\bin\Release\AddinRibbon.dll'
$installedAssembly = Join-Path $hostDir 'Plugins\AddinRibbon\AddinRibbon.dll'

# Incremental Build compiles changed source while retaining an unchanged installed
# binary's checksum. Rebuild would change timestamps in nondeterministic binaries.
& $buildTool (Join-Path $repoRoot 'AddinRibbon.sln') /t:Build '/p:Configuration=Release' '/p:Platform=Any CPU' "/p:NavisworksInstallDir=$hostDir" /m /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "PathFinder source build failed (exit $LASTEXITCODE)." }
foreach ($projectName in @('SmokePlugin.csproj', 'Runner.csproj')) {
    & $buildTool (Join-Path $testsRoot $projectName) /t:Rebuild '/p:Configuration=Release' '/p:Platform=x64' "/p:NavisworksInstallDir=$hostDir" "/p:PathFinderAssemblyPath=$builtAssembly" /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Native smoke project $projectName failed to build (exit $LASTEXITCODE)." }
}
$runnerPath = Join-Path $testsRoot 'bin\Release\PathFinder.Navisworks.SmokeRunner.exe'
$smokeAssembly = Join-Path $testsRoot 'bin\Release\PathFinder.Navisworks.SmokePlugin.dll'
if ($BuildOnly) {
    Write-Host "Native smoke helpers built at $testsRoot\bin\Release. No Navisworks instance was started."
    return
}

if (-not (Test-Path -LiteralPath $installedAssembly -PathType Leaf)) {
    throw 'Install the current verified PathFinder package before running native smoke tests.'
}
$builtHash = (Get-FileHash -LiteralPath $builtAssembly -Algorithm SHA256).Hash
$installedHash = (Get-FileHash -LiteralPath $installedAssembly -Algorithm SHA256).Hash
if ($builtHash -ne $installedHash) {
    throw 'Installed AddinRibbon.dll differs from the current Release build. Close Navisworks and install the current package before testing; an older registered assembly would invalidate the result.'
}
$priorRoamers = @(Get-CimInstance Win32_Process -Filter "Name='Roamer.exe'")
$expectedRoamer = [System.IO.Path]::GetFullPath((Join-Path $hostDir 'Roamer.exe'))
# Other installed years/products, such as Freedom, use the same process name.
# Keep those sessions untouched; block only this Manage host or an unknown path.
if (@($priorRoamers | Where-Object { [string]::IsNullOrWhiteSpace($_.ExecutablePath) -or
    $_.ExecutablePath.Equals($expectedRoamer, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0) {
    throw 'Close Navisworks Manage 2027 before the isolated native test. Existing user sessions are never stopped by this script.'
}
if ([string]::IsNullOrWhiteSpace($ModelPath)) { $ModelPath = Join-Path $hostDir 'Samples\snowmobile.nwd' }
$modelFile = [System.IO.Path]::GetFullPath($ModelPath)
if (-not (Test-Path -LiteralPath $modelFile -PathType Leaf)) { throw "Smoke model is missing: $modelFile." }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $runName = 'navisworks-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $OutputDirectory = Join-Path (Join-Path $repoRoot '.test-output') $runName
}
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$resultPath = Join-Path $outputRoot 'result.json'
$stdoutPath = Join-Path $outputRoot 'runner.stdout.log'
$stderrPath = Join-Path $outputRoot 'runner.stderr.log'
if (Test-Path -LiteralPath $resultPath) { throw "Refusing to replace an earlier native result at $resultPath. Choose an empty output directory." }
$arguments = @($hostDir, $modelFile, $installedAssembly, $smokeAssembly, $resultPath)
if ($Mode -ne 'smoke') { $arguments += $Mode }
# These are existing Windows file paths, with no embedded quotes or trailing slash.
# Quote each argument explicitly because Start-Process joins its ArgumentList.
foreach ($argument in $arguments) {
    if ($argument.Contains('"') -or $argument.EndsWith('\')) { throw 'Invalid native runner argument.' }
}
$argumentLine = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '

$startedUtc = [DateTime]::UtcNow
$ownedHosts = New-Object 'System.Collections.Generic.Dictionary[int,datetime]'
function Record-NewAutomationHosts {
    foreach ($candidate in @(Get-CimInstance Win32_Process -Filter "Name='Roamer.exe'")) {
        if ([string]::IsNullOrWhiteSpace($candidate.ExecutablePath)) { continue }
        if (-not $candidate.ExecutablePath.Equals($expectedRoamer, [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($candidate.CommandLine -notmatch '(?i)(?:^|\s)-Embedding(?:\s|$)') { continue }
        $createdUtc = $candidate.CreationDate.ToUniversalTime()
        if ($createdUtc -lt $startedUtc) { continue }
        $ownedHosts[[int]$candidate.ProcessId] = $createdUtc
    }
}

Write-Host "Running native $Mode checks in a fresh hidden Manage 2027 instance (limit $TimeoutSeconds seconds)."
$runner = Start-Process -FilePath $runnerPath -ArgumentList $argumentLine -WorkingDirectory $hostDir -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
$timer = [System.Diagnostics.Stopwatch]::StartNew()
$timedOut = $false
try {
    while (-not $runner.WaitForExit(500)) {
        Record-NewAutomationHosts
        if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) { $timedOut = $true; break }
    }
    if ($timedOut) {
        Record-NewAutomationHosts
        if (-not $runner.HasExited) { Stop-Process -Id $runner.Id -Force }
        foreach ($ownedId in @($ownedHosts.Keys)) {
            $candidate = Get-CimInstance Win32_Process -Filter "ProcessId=$ownedId" -ErrorAction SilentlyContinue
            if ($null -eq $candidate -or [string]::IsNullOrWhiteSpace($candidate.ExecutablePath)) { continue }
            if (-not $candidate.ExecutablePath.Equals($expectedRoamer, [StringComparison]::OrdinalIgnoreCase)) { continue }
            if ($candidate.CommandLine -notmatch '(?i)(?:^|\s)-Embedding(?:\s|$)') { continue }
            if ($candidate.CreationDate.ToUniversalTime() -ne $ownedHosts[$ownedId]) { continue }
            $nativeProcess = Get-Process -Id $ownedId -ErrorAction SilentlyContinue
            # A newly opened visible user window is never a cleanup target.
            if ($null -ne $nativeProcess -and $nativeProcess.MainWindowHandle -eq [IntPtr]::Zero) {
                Stop-Process -Id $ownedId -Force
            }
        }
        throw "Native test exceeded $TimeoutSeconds seconds. Only this runner and its newly recorded hidden automation hosts were targeted for cleanup. Logs: $outputRoot."
    }
    # WaitForExit(500) already reported completion, so this call only flushes logs.
    $runner.WaitForExit()
    if ($runner.ExitCode -ne 0) { throw "Native runner failed with exit $($runner.ExitCode). Inspect $resultPath and $stderrPath." }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw "Native runner produced no result: $resultPath." }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($Mode -ne 'extract-model') {
        $failed = @($result.checks | Where-Object { -not $_.passed })
        if ($failed.Count -gt 0) { throw "Native checks failed: $(($failed.name) -join ', '). Results: $resultPath." }
        Write-Host "Passed $(@($result.checks).Count) actual-host checks. Results and tab previews: $outputRoot."
    } else {
        Write-Host "Found $($result.routeNamedLeafCount) route-named geometry leaves; retained $($result.inspectedRecordCount) bounding-box records (cap $($result.recordCap), truncated: $($result.truncated)). Result: $resultPath."
    }
} finally {
    $runner.Dispose()
}
