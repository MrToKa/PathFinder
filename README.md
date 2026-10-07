# PathFinder — Navisworks Manage 2027

Two-tab cable routing proof of concept, version 1.1.3, for the Navisworks Manage 2027 .NET API (24.x), .NET Framework 4.8, x64. Selected routes form a 3D graph in metres; its edge lengths and endpoint attachments determine the cable length between From and To.

## Use

Open a 3D model, then choose **Path Finder → Tool Panel**.

1. On **Routes**, select tray containers or individual tray leaves in the Navisworks Selection Tree. Choose **MV**, **LV**, and/or **Control**, then click **Add / update selection**. Every deepest descendant with geometry is included; selecting a leaf itself also works. The grid shows each selected root, its geometry-leaf count, and editable permissions. A later rule overrides an earlier rule for overlapping leaves. **Remove rule** removes selected grid rows.
2. On **Path**, enter exact **From** and **To** display names, preserving any initial `=`. Lookup uses only currently visible objects with visible geometry; hidden objects and descendants of hidden parents are skipped. Alternatively select exactly one visible model object and press its **Use selection** button. This resolves multiple visible objects with the same display name and unnamed imported geometry. Choose the cable type and press **Calculate path**.
3. The result lists route codes in traversal order and estimated length. `/SECONDARY` is inserted at the From and/or To end when the endpoint centre is strictly more than **2 metres** from the closest allowed route centreline. Exactly 2 m does not trigger it.
4. **Show path** makes From green, To orange, and route geometry blue, all at 0% transparency. Every other model object receives 95% transparency. A yellow world-anchored 3D line displays the calculated cable path, including endpoint attachments; it remains visible over the solids. If From and To overlap, shared geometry is purple. **Restore view** removes the line and restores the previous visible temporary materials. Permanent materials and hidden states are preserved.
5. **Reverse** swaps From/To and reverses the result, including SECONDARY markers. A displayed path updates its endpoint colours and line direction.

Name lookup runs a native Navisworks search for each requested name and caches its matches, then checks their current visibility on every lookup. Leading/trailing whitespace and letter case are ignored. It does not build a managed index of every name in the model.

**Pause automatic calculation** starts checked. Selection changes never trigger path calculation or file refresh. The manual button works while paused. If you explicitly uncheck Pause, changes to valid From/To fields, cable type, or connection tolerance start a calculation after a 700 ms delay. Model capture is batched on the Navisworks UI thread; graph calculation uses detached values on a worker. **Cancel** stops capture/calculation. There is no automatic `UpdateFiles()` call.

Route rules belong to the current pane/model session. Opening/replacing/appending a model, changing model transforms or document units, or updating the model clears stale rules/results. Re-add the route selection afterward.

Hidden tray leaves are excluded from the calculated network. **Hide/Unhide** clears any calculated/displayed path while preserving route rules and picked endpoint identities. Recalculate to use the current visible objects. Unhidden trays participate again without re-adding their rules; a hidden picked endpoint must be replaced or unhidden. The grid's leaf count describes the complete assigned hierarchy, including leaves currently hidden. Visibility means Navisworks Hidden state, including ancestors; transparency and camera occlusion do not hide an object for routing.

## Geometry and engineering limits

Straight leaves use the longest world-space bounding-box axis as their approximate centreline. Named **BEND / ELBOW** components can instead produce a curved centreline when they have thin, axis-aligned, square bounds and one uniquely validated pair of orthogonal ports. Both ports must match actual mesh face extents and neighbouring named straight arms, including their cross-sections. Equal radial offsets define a 90° turn, represented by 18 five-degree arc pieces. This supports XY, XZ and YZ bends in all four turn orientations.

Native mesh reads are restricted to identified bends, with primitive/fragment limits. Detached face evidence is cached with its bounds and active transform. Unsupported, missing or ambiguous evidence retains the straight approximation and increments a fallback count. A successful result displays this count for the selected network when review is needed. Reconstructing a bend preserves physical port positions and any remaining gap between components.

An AABB tree finds candidate polyline pieces; connections use their analytical closest contacts. The sparse graph contains original polyline vertices, endpoint projections and contacts, without regular sample points. Validated bends connect to other trays only through their two physical ports, preserving the curve at larger connection tolerances; equipment endpoints may still project to the curve's interior. Collinear overlaps connect at both overlap boundaries. A 10 m + 10 m perpendicular pair retains its full 20 m length at both 0.25 m and 1.30 m tolerance. Node limits count actual graph vertices rather than samples per metre.

Neighbouring pieces connect within the chosen **Connection gap (m)**: 0.25 m by default, adjustable from 0.01 to 1.30 m. A larger gap may connect nearby parallel trays; proximity does not prove a physical connector. Disconnected nearest eligible trays produce a no-path result with their codes, attachment distances and chosen tolerance. Forbidden cable categories are excluded before both endpoint attachment and graph construction.

Lengths remain approximate. Arbitrarily rotated straights, other bend angles, non-planar curves, tees and complex fittings need a geometry review; supported quarter arcs are inferred from mesh ports rather than extracted CAD curves. Equipment attachments use the centre of its visible bounding box. `/SECONDARY` is a textual attachment marker and does not create model geometry. Cached paths are checked against both current bounds and active transforms before Show, including rotations that leave the bounding box unchanged.

Show path captures and restores the whole model's appearance synchronously on the native UI thread. A historical full-model metadata inspection found 376,727 geometry pieces and exceeded its 300-second limit; that run does not measure the current per-name search, bend-only mesh capture or sparse graph. Full-project visualization performance remains unverified. Start with selected tray subsets. Graph size and candidate limits fail clearly for excessive or dense networks; dense physical contacts can still require quadratic work. Pause remains checked by default.

Names preserve full codes such as `/BC001`, `/DFBC009`, `/VFD007-2`. Rules are explicit; names do not grant cable permissions automatically. The supplied tray catalog supports A→MV, B/DFB→LV, BC/DFBC→LV+Control, C/DFC→Control. VFD and grounding require an explicit user decision within this three-category PoC.

## Build and test

Run `scripts/Build.ps1` in PowerShell. It discovers Manage 2027 and Visual Studio MSBuild; override with `-NavisworksInstallDir` and `-MSBuildPath` if needed. Build output is `AddinRibbon/bin/Release`. The installed SDK assemblies `Autodesk.Navisworks.Api.dll`, `Autodesk.Navisworks.ComApi.dll` and `Autodesk.Navisworks.Interop.ComApi.dll` are required; each is referenced with `Private=False` and is never copied into the plugin. Build does not deploy automatically.

Run `dotnet run --project tests/PathFinder.Routing.Tests -- <optional-local-excel-fixture.json>` for the package-free routing and geometry harness. Version 1.1.3 passed 30 checks including all 327 local Excel tray-code fixtures; without the optional fixture there are 29 checks. The linked production routing code is tested directly, including supported bends, conservative fallback and preservation of the full arc at 0.25 m tolerance. Private fixture data is excluded from the public repository/package.

After installing the matching Release DLL and closing Navisworks, run `scripts/Test-Navisworks.ps1` inside a fresh hidden Manage 2027 instance; it writes local results, pane previews and scene/overlay images without saving the model. Version 1.1.3 passed all 34 native checks, including name-cache reuse, unchanged-box rotation detection, the full manual UI flow, visibility, appearance and the rendered cable line. The tested, installed and packaged DLLs match. See the validation notes for current evidence.

A separate read-only candidate run in the supplied project passed 13 checks on 869 visible assigned leaves at the default 0.25 m gap. A previously disconnected quarter bend connected through its physical ports, retaining all 19 curve vertices; forward/reverse lengths agreed with an independent full-segment calculation. Cold name searches took 0.319 s and 0.236 s, cached searches under 1 ms; geometry capture took 2.87 s initially and 0.267 s with cached mesh evidence. These measurements cover that selected network, not full-model visualization. Fourteen bends were validated and 89 retained the reported approximation fallback.

The active UI is `AddinRibbon/Ctr/PathFinderControl.cs`, created by `ClDockPanelUpdate`. Legacy `Algo.Designer.cs` and other old controls remain as reference source and are excluded from the current project. The dock explicitly allows resizing, starts at a useful size and reapplies Dock=Fill after native-parent attachment; font scaling has an explicit baseline.

Run `scripts/Package.ps1 -Destination <new-empty-directory>` to create the DLL/XAML/icon package and checksums. Close Manage 2027, then run the package's `scripts/Install.ps1 -PackageDirectory <package-directory>`. The installer writes `Plugins/AddinRibbon` beneath Manage 2027 and backs up any previous folder under `PathFinderBackups`. Restart Navisworks to load the DLL. Do not install it in Freedom or use a different year's Autodesk API libraries.

See [action plan](docs/ActionPlan.md) and [validation notes](docs/Validation.md) for implementation scope and test evidence.

In Manage 2027, temporary material overrides mark the document as modified. Restore view restores its previous appearance, while the native modified flag remains set. The add-on does not save the model or clear that flag.
