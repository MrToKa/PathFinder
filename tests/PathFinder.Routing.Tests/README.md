# Routing regression harness

This executable compiles the production routing sources directly, using C# 7.3,
without a Navisworks dependency or third-party test packages. It targets .NET 9
only to make the detached routing core independently executable.

Run from the repository root:

```powershell
dotnet run --project tests/PathFinder.Routing.Tests/PathFinder.Routing.Tests.csproj
```

To additionally validate all 327 tray names extracted from the supplied Rev. 05
Excel report, pass the locally generated report fixture. The fixture remains
outside the repository because it contains project data:

```powershell
dotnet run --project tests/PathFinder.Routing.Tests/PathFinder.Routing.Tests.csproj -- ../excel_route_fixtures.json
```

The checks cover complete route codes and piece suffixes, polyline bends,
interior endpoint projection on long trays, the strict `> 2 m` SECONDARY
boundary, gaps and Z separation, exact angled contacts between samples at the
connection tolerance boundary, full right-angle lengths at both configured
tolerances, known diagonal lengths in three dimensions, interior junctions,
parallel overlap in both directions, category restrictions, route re-entry,
reversal, repeated calculation, invalid input, graph node and connection limits, cancellation
before/during work, and a 45 x 45 grid of intersecting trays.

The latest run on 7 October 2026 passed all 17 checks including the Excel
fixture. The grid used 90 polylines and approximately 31,770 regular samples;
calculation completed in 149 ms on the development machine and followed the
88 m orthogonal route without cutting corners. This is a detached
geometry benchmark, not a Navisworks model-load or display benchmark.

Each calculation creates its own graph. Cross-tray links are restricted to
the selected tolerance and eligible cable category. A spatial hash uses samples
only to discover nearby original polyline pieces; cross-tray links join their
analytical closest contacts. Arbitrary neighbouring samples cannot shortcut a
bend. Parallel pieces connect at both overlap boundaries to preserve continuous
travel. Contact points are inserted
into the continuous tray geometry rather than connected to an arbitrary tray
endpoint. Only consecutive route codes are collapsed, so `B001/BC001/B001`
retains the return to B001. Reversal reverses the existing result, including
endpoint-specific SECONDARY markers.

The core evaluates the polylines supplied by its caller. It cannot recover
rotated tray axes, elbow curves, fittings, or physical connections omitted by
the Navisworks bounding-box adapter. The host acceptance test must verify
those parts against the actual model.
