# PathFinder — Navisworks Manage 2027

Two-tab cable routing proof of concept, version 1.1.1, for the Navisworks Manage 2027 .NET API (24.x), .NET Framework 4.8, x64. Selected routes form a 3D graph in metres; its edge lengths and endpoint attachments determine the cable length between From and To.

## Use

Open a 3D model, then choose **Path Finder → Tool Panel**.

1. On **Routes**, select tray containers or individual tray leaves in the Navisworks Selection Tree. Choose **MV**, **LV**, and/or **Control**, then click **Add / update selection**. Every deepest descendant with geometry is included; selecting a leaf itself also works. The grid shows each selected root, its geometry-leaf count, and editable permissions. A later rule overrides an earlier rule for overlapping leaves. **Remove rule** removes selected grid rows.
2. On **Path**, enter exact **From** and **To** display names, preserving any initial `=`. Alternatively select exactly one model object and press its **Use selection** button. This also resolves duplicate display names and unnamed imported geometry. Choose the cable type and press **Calculate path**.
3. The result lists route codes in traversal order and estimated length. `/SECONDARY` is inserted at the From and/or To end when the endpoint centre is strictly more than **2 metres** from the closest allowed route centreline. Exactly 2 m does not trigger it.
4. **Show path** makes From green, To orange, and route geometry blue, all at 0% transparency. Every other model object receives 95% transparency. A yellow world-anchored 3D line displays the calculated cable path, including endpoint attachments; it remains visible over the solids. If From and To overlap, shared geometry is purple. **Restore view** removes the line and restores the previous visible temporary materials. Permanent materials and hidden states are preserved.
5. **Reverse** swaps From/To and reverses the result, including SECONDARY markers. A displayed path updates its endpoint colours and line direction.

**Pause automatic calculation** starts checked. Selection changes never trigger path calculation or file refresh. The manual button works while paused. If you explicitly uncheck Pause, changes to valid From/To fields, cable type, or connection tolerance start a calculation after a 700 ms delay. Model capture is batched on the Navisworks UI thread; graph calculation uses detached values on a worker. **Cancel** stops capture/calculation. There is no automatic `UpdateFiles()` call.

Route rules belong to the current pane/model session. Opening/replacing/appending a model, changing model transforms or document units, or updating the model clears stale rules/results. Re-add the route selection afterward.

## Geometry and engineering limits

This PoC derives a centreline from the longest world-space bounding-box axis of each leaf, then joins neighbouring centreline pieces within the chosen **Connection gap (m)**. The default is 0.25 m; the UI allows 0.01–1.30 m. A larger gap may connect nearby parallel trays; choose it for the geometry being reviewed. Disconnected nearest eligible trays produce a clear no-path result. Forbidden cable categories are excluded before both endpoint attachment and graph construction.

Samples discover candidate pieces. Connections use their analytical closest contacts, rather than links between arbitrary nearby samples that could cut a corner. Collinear overlaps connect at both overlap boundaries. A 10 m + 10 m perpendicular pair therefore retains its full 20 m length at both 0.25 m and 1.30 m tolerance. Proximity alone still does not prove a physical connector in the source model.

Bounding boxes do not contain the actual CAD centreline. Rotated pieces, elbows, curved parts, tray thickness, and equipment connection points therefore need a geometry review; displayed lengths are approximate. The 2 m test uses the equipment bounding-box centre and the approximated route centreline. `/SECONDARY` is a textual attachment marker; it does not create new model geometry. This is suitable for testing the proposed workflow, with exact curve extraction left as a later engineering improvement.

Very large models also need a performance review. Show path captures and restores appearance synchronously on the native UI thread. A read-only inspection of the supplied project found 376,727 geometry pieces and exceeded its 300-second limit before completing all leaf metadata; full-project routing and visualization performance are not verified. Start with selected tray subsets. Graph size limits fail clearly when a network is too large or dense, and Pause remains checked by default.

Names preserve full codes such as `/BC001`, `/DFBC009`, `/VFD007-2`. Rules are explicit; names do not grant cable permissions automatically. The supplied tray catalog supports A→MV, B/DFB→LV, BC/DFBC→LV+Control, C/DFC→Control. VFD and grounding require an explicit user decision within this three-category PoC.

## Build and test

Run `scripts/Build.ps1` in PowerShell. It discovers Manage 2027 and Visual Studio MSBuild; override with `-NavisworksInstallDir` and `-MSBuildPath` if needed. Build output is `AddinRibbon/bin/Release`. It does not deploy automatically, and Autodesk DLLs are never copied into the plugin.

Run `dotnet run --project tests/PathFinder.Routing.Tests -- <optional-local-excel-fixture.json>` for the 17-check package-free routing regression harness. The linked production routing code is tested directly. The fixture JSON is an optional local extraction and is not part of the public repository/package. After installing the matching Release DLL and closing Navisworks, run `scripts/Test-Navisworks.ps1` for 24 checks inside a fresh hidden Manage 2027 instance; it writes local results, pane previews and scene/overlay images without saving the model.

The active UI is `AddinRibbon/Ctr/PathFinderControl.cs`, created by `ClDockPanelUpdate`. Legacy `Algo.Designer.cs` and other old controls remain as reference source and are excluded from the current project. The dock explicitly allows resizing, starts at a useful size and reapplies Dock=Fill after native-parent attachment; font scaling has an explicit baseline.

Run `scripts/Package.ps1 -Destination <new-empty-directory>` to create the DLL/XAML/icon package and checksums. Close Manage 2027, then run the package's `scripts/Install.ps1 -PackageDirectory <package-directory>`. The installer writes `Plugins/AddinRibbon` beneath Manage 2027 and backs up any previous folder under `PathFinderBackups`. Restart Navisworks to load the DLL. Do not install it in Freedom or use a different year's Autodesk API libraries.

See [action plan](docs/ActionPlan.md) and [validation notes](docs/Validation.md) for implementation scope and test evidence.

In Manage 2027, temporary material overrides mark the document as modified. Restore view restores its previous appearance, while the native modified flag remains set. The add-on does not save the model or clear that flag.
