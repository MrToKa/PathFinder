# Native Manage 2027 smoke checks

These independent .NET Framework 4.8/x64 projects run the production add-on
inside an isolated, hidden Navisworks Manage 2027 instance. They are excluded
from the main solution and release package. Autodesk libraries are referenced
from the installed application and are never copied into the helper output.

First build and install the current PathFinder package using the repository
scripts, then close all Navisworks sessions. From the repository root:

```powershell
./scripts/Test-Navisworks.ps1
```

The default model is the installed `Samples/snowmobile.nwd`. To choose another
model or an installation that cannot be discovered automatically:

```powershell
./scripts/Test-Navisworks.ps1 -NavisworksInstallDir 'D:\Programs\Navisworks Manage 2027' -ModelPath 'D:\Models\sample.nwd'
```

The script builds changed source and rebuilds the helper projects, checks that the installed
`AddinRibbon.dll` has the same SHA-256 as the current Release output, and rejects
an existing Roamer session. Different registered DLLs must be replaced before
testing; adding a newer assembly to an already registered old add-on can load
the wrong implementation.

To compile the helpers without starting Navisworks or checking installation:

```powershell
./scripts/Test-Navisworks.ps1 -BuildOnly
```

The runner calls the smoke plugin for 24 checks in the native host. It checks the deepest
leaf traversal and overlapping rules; metre conversion; missing/duplicate
objects; cancellation; model-transform invalidation and captured item
fingerprints; temporary appearance, opacity, reversal, restoration and
disposal; dock registration; two tabs, the Pause default and manual calculation;
the actual Assign/Pick/Calculate/Show/Reverse/Restore UI handlers; native docking
metadata and parent reset/resize/reparent layout; render-plugin loading, units,
bounds and a visible yellow cable line in ScenePlusOverlay compared with Scene;
and overlay clearing on Restore, edit and Dispose. The
sample must contain at least five geometry leaves, a nested root and duplicate
display names. The helper changes only the isolated in-memory document and
does not save it. Its permanent-material assertions deliberately alter the
test session before validating temporary-view restoration.

Results, logs, tab/parented previews and scene/overlay images are written into a unique directory under
the gitignored `.test-output/`. A custom `-OutputDirectory` is supported. Results
include model names and native error details, so review them before sharing.

The runner has a maximum 300-second deadline. On timeout the script stops its
own runner and revalidates only newly recorded, hidden `-Embedding` Roamer
processes from the selected installation before cleanup. It never stops a
pre-existing or visible user session. Do not open a second automation test
during this isolated run.

An optional read-only project extraction returns route names and geometry
bounding boxes in metres, without running destructive in-memory assertions:

```powershell
./scripts/Test-Navisworks.ps1 -Mode extract-model -ModelPath 'D:\Models\project.nwd'
```

It counts all visited route-named leaves, retains at most 5,000 bounding-box
records, and reports `truncated` and omitted counts explicitly. A companion
progress JSON records the traversal stage. The same 300-second deadline applies;
a timeout leaves progress evidence, not a completed model-validation result.

The native model checks complement the detached routing regressions. They do
not make bounding-box centreline estimates into exact CAD curve measurements.
