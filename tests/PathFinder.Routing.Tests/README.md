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
boundary, gaps and Z separation, exact angled contacts and diagonal AABB gaps at the
connection tolerance boundary, full right-angle lengths at both configured
tolerances, known diagonal lengths in three dimensions, interior junctions,
parallel overlap in both directions, category restrictions, route re-entry,
reversal, repeated calculation, invalid input, sparse graph node and connection limits,
rejected candidate-pair limits, cancellation before/during work, 1,000 coincident pieces,
10,000 long parallel pieces with scrambled identifiers, and a 45 x 45 grid of intersecting trays.

Seven geometry checks additionally cover mesh-validated quarter bends in XY, XZ and YZ
with all four turn orientations, arc length and category preservation, changed mesh ports
after a half-turn with unchanged bounds, missing evidence/arms, ambiguous ports, unequal
radii and non-planar fallback, preserved physical gaps, and cancellation. The combined
Release run on 7 October 2026 passed all 28 checks, including all 327 local Excel tray-code
fixtures. The 90-polyline grid followed its exact 88 m route in 5 ms using 89 path points.

The sparse implementation removes regular sample vertices. On the development machine,
a chain of 1,000 straight 20 m pieces changed from 275–404 ms, approximately 104 MiB
allocated and 160,001 path points to 8 ms, 1.2 MiB and 1,001 physical joints. A separate
parallel network of 1,000 pieces changed from 222–297 ms and 87 MiB to 6 ms and 1 MiB.
These are detached geometry benchmarks, not Navisworks model-load or display benchmarks.
Dense coincident geometry still has quadratic physical contact combinations: 1,000
identical 10 m pieces require approximately 6.3 seconds and 147 MiB here, so size and
candidate limits remain necessary.

Each calculation creates its own graph. Cross-tray links are restricted to
the selected tolerance and eligible cable category. A balanced AABB tree uses piece
centres to find nearby original polyline pieces; cross-tray links join their analytical
closest contacts. The graph contains original vertices, endpoint projections and contacts.
It retains actual bends and their complete length. Parallel pieces connect at both
overlap boundaries to preserve continuous travel. Contact points are inserted
into the continuous tray geometry rather than connected to an arbitrary tray
endpoint. Only consecutive route codes are collapsed, so `B001/BC001/B001`
retains the return to B001. Reversal reverses the existing result, including
endpoint-specific SECONDARY markers.

The node limit now counts actual vertices/projections/contacts, rather than samples per
metre. The connection limit counts distinct graph edges. At most eight times the configured
connection limit is allowed in candidate piece-pair comparisons; this also bounds rejected
contacts whose overlapping boxes would otherwise evade an edge limit. Sample spacing is
accepted and validated for compatibility, but no longer changes topology or graph size.
Disconnected errors identify the nearest route codes, object attachment distances and
configured tolerance. A disconnected nearest tray does not silently select another island.

The core evaluates the polylines supplied by its caller. The geometry builder can reconstruct
named, axis-aligned 90-degree bends only when mesh face extents and neighbouring straight
ports validate one unique fit; its 18 arc pieces approximate the circular length. Unsupported
or ambiguous bends retain the world-box-axis fallback and report diagnostics. Arbitrary
rotated tray axes, other elbow curves, fittings and physical connections still require native
model validation. Detached geometry checks do not verify native mesh extraction, live
visibility events, transform fingerprints or UI-thread responsiveness.
