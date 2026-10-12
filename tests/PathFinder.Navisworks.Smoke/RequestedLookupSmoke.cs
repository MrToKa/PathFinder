using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using AddinRibbon.Ctr;
using AddinRibbon.Routing;
using AddinRibbon.Services;
using Autodesk.Navisworks.Api;

public sealed partial class PathFinderSmoke
{
    // The external specification and diagnostics stay beside the local smoke
    // result. No supplied model, entered project paths, or detached geometry is
    // embedded into the committed test source or saved back into the model.
    private void TestRequestedPathLookup(string specPath)
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var specification = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(specPath));
        Assert(specification.ContainsKey("connectionGapMeters") && specification.ContainsKey("paths"),
            "The lookup specification needs connectionGapMeters and paths.");
        decimal connectionGap = Convert.ToDecimal(specification["connectionGapMeters"], CultureInfo.InvariantCulture);
        var entries = specification["paths"] as IEnumerable;
        Assert(entries != null && !(entries is string), "Specification paths must be an array of entered routes.");
        var paths = entries.Cast<object>().Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)).ToArray();
        Assert(paths.Length > 0 && paths.All(path => !string.IsNullOrWhiteSpace(path)), "The lookup specification contains no paths or an empty path.");
        var probes = new List<ModelItem>();
        var slice = Stopwatch.StartNew();
        using (var roots = document.Models.CreateCollectionFromRootItems())
            foreach (var item in VisibleProbeItems(roots))
            {
                if (item.HasGeometry && !item.Children.Any()) probes.Add(item);
                if (probes.Count >= 67) break;
                if (slice.ElapsedMilliseconds >= 20) { System.Windows.Forms.Application.DoEvents(); slice.Restart(); }
            }
        Assert(probes.Count > 0, "The opened model has no visible geometry for bounded appearance checks.");
        geometry = probes;
        var rows = new List<Dictionary<string, object>>();
        var metrics = new Dictionary<string, object>
        {
            { "documentUnits", document.Units.ToString() },
            { "metresPerDocumentUnit", UnitConversion.ScaleFactor(document.Units, Units.Meters) },
            { "connectionGapMeters", connectionGap }, { "sampleGeometryLeaves", probes.Count },
            { "geometryValidationScope", "exact used-leaf selection and overlay; at most 67 background/appearance probes" },
            { "paths", rows }
        };
        string metricsPath = Path.Combine(Path.GetDirectoryName(output), "requested-path-lookup-metrics.json");
        for (int index = 0; index < paths.Length; index++)
        {
            int request = index + 1;
            string input = paths[index];
            var row = new Dictionary<string, object>
            {
                { "request", request }, { "input", input }, { "passed", false },
                { "phase", "starting" }, { "capturedGeometryLeaves", 0 }, { "usedGeometryLeaves", 0 },
                { "showMilliseconds", -1L }, { "warmShowMilliseconds", -1L }, { "restoreMilliseconds", -1L }
            };
            rows.Add(row);
            File.WriteAllText(metricsPath, serializer.Serialize(metrics));
            Check("requested_path_lookup_" + request + "_selects_visible_ordered_geometry_overlay_restore_and_dispose", () =>
            {
                PathFinderControl control = null;
                RoutingSession session = null;
                var originalSelection = document.CurrentSelection.SelectedItems.ToArray();
                var before = Snapshot(probes);
                var hidden = probes.SelectMany(item => item.AncestorsAndSelf).Distinct().ToDictionary(item => item, item => item.IsHidden);
                modifiedBeforeVisualization = document.IsModified;
                var clock = new Stopwatch();
                try
                {
                    string expected = string.Concat(RouteLookupCalculator.Parse(input));
                    row["expectedRouteText"] = expected;
                    control = new PathFinderControl();
                    control.Size = new System.Drawing.Size(800, 700); control.CreateControl();
                    Field<TabControl>(control, "tabs").SelectedIndex = 2;
                    var gap = Field<NumericUpDown>(control, "gap");
                    Assert(connectionGap >= gap.Minimum && connectionGap <= gap.Maximum, "Specified Connection gap is outside the Path field range.");
                    gap.Value = connectionGap;
                    Field<TextBox>(control, "lookupInput").Text = input;
                    Assert(Field<TextBox>(control, "from").Text.Length == 0 && Field<TextBox>(control, "to").Text.Length == 0,
                        "Entered-route lookup unexpectedly needed From/To equipment.");
                    session = Field<RoutingSession>(control, "lookupSession");
                    Assert(session.GeometryBuildCount == 0 && !Field<bool>(control, "busy"), "Entering the path automatically started calculation.");
                    row["phase"] = "showing"; File.WriteAllText(metricsPath, serializer.Serialize(metrics));
                    clock.Restart(); Wait((Task)Invoke(control, "ShowLookupAsync")); clock.Stop();
                    row["showMilliseconds"] = clock.ElapsedMilliseconds;
                    row["capturedGeometryLeaves"] = session.SegmentItems.Count;
                    row["message"] = Field<TextBox>(control, "lookupOutput").Text;
                    var result = Field<RouteResult>(control, "lookupResult");
                    Assert(result != null && result.Success, "Requested path failed: " + Field<TextBox>(control, "lookupOutput").Text);
                    Assert(result.RouteText == expected, "Lookup omitted/reordered a route or retained SECONDARY: " + result.RouteText);
                    Assert(result.FromDistanceMeters == 0 && result.ToDistanceMeters == 0
                        && !result.FromRequiresSecondary && !result.ToRequiresSecondary, "Lookup fabricated SECONDARY/equipment approach geometry.");
                    Assert(session.Assignments.Count == 0 && Field<RoutingSession>(control, "session").Assignments.Count == 0,
                        "Requested lookup required or created Routes assignments.");
                    var selected = result.SegmentIds.Select(id => session.SegmentItems[id]).Distinct().ToArray();
                    Assert(selected.Length > 0 && selected.All(item => item.HasGeometry && !item.Children.Any() && RoutingSession.IsVisible(item)),
                        "Lookup selected hidden objects or objects above the deepest geometry level.");
                    AssertSelection(selected); AssertOverlayPoints(result.PathPoints);
                    Assert(result.LengthMeters > 0 && result.PathPoints.Count > 1, "Successful requested path has no non-zero yellow cable line.");
                    Assert(Field<PathVisualization>(control, "visualization").IsPathSelected
                        && Field<Button>(control, "lookupRestore").Enabled, "Lookup route selection/Restore availability is incorrect.");
                    foreach (var item in selected) { AssertColor(item, 0.10, 0.65, 1.00); AssertNear(item.Geometry.ActiveTransparency, 0); }
                    AssertAppearance(before.Where(snapshot => !selected.Contains(snapshot.Item)).ToList(), true, true);
                    AssertAppearance(before.Where(snapshot => selected.Contains(snapshot.Item)).ToList(), false, true);
                    Assert(hidden.All(pair => pair.Key.IsHidden == pair.Value), "Lookup changed bounded hidden flags.");
                    modifiedAfterVisualization = document.IsModified;
                    row["routeText"] = result.RouteText; row["usedGeometryLeaves"] = selected.Length;
                    row["routesCaptured"] = result.RouteCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    row["routeLengthMeters"] = result.LengthMeters; row["pathPoints"] = result.PathPoints.Count;
                    row["connectionGaps"] = result.ConnectionGapCount; row["connectionGapLengthMeters"] = result.ConnectionGapLengthMeters;
                    SavePreview(control, "pathfinder-requested-lookup-" + request + "-result.png");
                    row["phase"] = "restoring"; clock.Restart(); Invoke(control, "RestoreView"); clock.Stop();
                    row["restoreMilliseconds"] = clock.ElapsedMilliseconds;
                    AssertSelection(originalSelection); AssertAppearance(before, true, true);
                    Assert(RoutePathOverlay.DisplayedPoints.Count == 0 && !Field<PathVisualization>(control, "visualization").IsShown,
                        "Restore left the requested route's yellow overlay or temporary view active.");
                    row["phase"] = "showing before dispose";
                    clock.Restart(); Wait((Task)Invoke(control, "ShowLookupAsync")); clock.Stop();
                    row["warmShowMilliseconds"] = clock.ElapsedMilliseconds;
                    var repeated = Field<RouteResult>(control, "lookupResult");
                    Assert(repeated != null && repeated.Success && repeated.RouteText == expected, "Repeated requested-route lookup failed before Dispose.");
                    AssertSelection(repeated.SegmentIds.Select(id => session.SegmentItems[id]).Distinct().ToArray());
                    AssertOverlayPoints(repeated.PathPoints);
                    control.Dispose(); control = null;
                    AssertSelection(originalSelection); AssertAppearance(before, true, true);
                    Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Dispose left the requested route yellow overlay.");
                    Assert(hidden.All(pair => pair.Key.IsHidden == pair.Value), "Restore/Dispose changed bounded hidden flags.");
                    row["passed"] = true; row["phase"] = "complete";
                }
                catch (Exception error)
                {
                    row["error"] = error.ToString();
                    if ((long)row["showMilliseconds"] < 0) row["showMilliseconds"] = clock.ElapsedMilliseconds;
                    if (session != null)
                    {
                        row["capturedGeometryLeaves"] = session.SegmentItems.Count;
                        var fitted = Field<TrayGeometryBuildResult>(session, "fittedGeometry");
                        if (fitted != null)
                            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "requested-lookup-" + request + "-detached-diagnostic.json"),
                                serializer.Serialize(fitted.Segments.Select(segment => new
                                {
                                    id = segment.Id, route = segment.RouteCode, points = segment.Points,
                                    endsOnly = segment.ConnectionsAtEndsOnly, verifiedPorts = segment.VerifiedPorts,
                                    measuredClearanceSurfaces = segment.ConnectionSurfaces.Count
                                }).ToArray()));
                    }
                    throw;
                }
                finally
                {
                    control?.Dispose();
                    document.CurrentSelection.CopyFrom(originalSelection);
                    modifiedAfterRestore = document.IsModified;
                    row["documentModifiedBeforeVisualization"] = modifiedBeforeVisualization;
                    row["documentModifiedAfterVisualization"] = modifiedAfterVisualization;
                    row["documentModifiedAfterRestore"] = modifiedAfterRestore;
                    File.WriteAllText(metricsPath, serializer.Serialize(metrics));
                }
            });
        }
    }
}
