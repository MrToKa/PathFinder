using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AddinRibbon.Ctr;
using AddinRibbon.Routing;
using AddinRibbon.Services;
using Autodesk.Navisworks.Api;

public sealed partial class PathFinderSmoke
{
    // Append this deterministic public fixture only after the sample-model checks.
    // It changes the isolated document in memory; the source model is never saved.
    private void TestPathLookup(List<ModelItem> sampleLeaves)
    {
        LookupFixture fixture = null;
        Check("lookup_fixture_imports_logical_parent_component_and_duplicate_hierarchy", () =>
        {
            string path = Path.Combine(Path.GetDirectoryName(output), "path-lookup-fixture.ifc");
            File.WriteAllText(path, CreateLookupIfc(), new UTF8Encoding(false));
            int priorModels = document.Models.Count;
            Assert(document.TryAppendFile(path), "Manage could not append the generated IFC lookup fixture.");
            Assert(document.Models.Count == priorModels + 1, "Fixture append did not add exactly one model.");
            fixture = new LookupFixture(document.Models[priorModels].RootItem);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "path-lookup-import.json"),
                new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(fixture.All.Select(item => new
                {
                    name = item.DisplayName, geometry = item.HasGeometry, children = item.Children.Count(),
                    ancestors = item.Ancestors.Select(owner => owner.DisplayName).ToArray()
                }).ToArray()));
            Assert(fixture.Route("/LKP101").Count == 1 && fixture.Route("/LKP103").Count == 2,
                "Imported IFC did not preserve logical route parents and independent duplicate names.");
            Assert(fixture.Leaves.Count == 11 && fixture.Leaves.All(RoutingSession.IsVisible),
                "Fixture geometry leaves are missing or unexpectedly hidden: " + fixture.Leaves.Count);
            Assert(fixture.Route("/5LD04").Single().Descendants.Any(item => item.DisplayName == "BRANCH /5LD04/B1"),
                "Imported IFC lost the qualified component hierarchy.");
        });
        if (fixture == null) return;

        Check("lookup_captures_visible_logical_routes_without_assignments_and_reuses_warm_geometry", () =>
        {
            using (var session = new RoutingSession())
            {
                var codes = new[] { "/LKP101", "/LKP102", "/5LD04" };
                var segments = Wait(session.CaptureLookupSegmentsAsync(codes, CancellationToken.None));
                Assert(session.Assignments.Count == 0, "Lookup created cable category assignments.");
                Assert(segments.Count == 6 && segments.Count(item => item.RouteCode == "/LKP101") == 3
                    && segments.Count(item => item.RouteCode == "/LKP102") == 1
                    && segments.Count(item => item.RouteCode == "/5LD04") == 2,
                    "Logical parent capture omitted leaves or used qualified component names as route labels.");
                Assert(session.SegmentItems.Values.All(item => !item.Children.Any() && RoutingSession.IsVisible(item)),
                    "Lookup returned a non-leaf or hidden native geometry object.");
                int builds = session.GeometryBuildCount;
                var warm = Wait(session.CaptureLookupSegmentsAsync(codes, CancellationToken.None));
                Assert(warm.Count == segments.Count && session.GeometryBuildCount == builds && session.AreCapturedSegmentsCurrent(),
                    "Warm manual lookup refitted unchanged geometry or discarded native fingerprints.");
            }
        });
        Check("lookup_reports_all_missing_and_ambiguous_names_without_partial_capture", () =>
        {
            using (var session = new RoutingSession())
            {
                Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP101" }, CancellationToken.None));
                var before = session.SegmentItems.ToDictionary(item => item.Key, item => item.Value);
                try
                {
                    Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP101", "/LKP103", "/LKP999", "/LKP998" }, CancellationToken.None));
                    throw new Exception("An ambiguous/missing route lookup unexpectedly succeeded.");
                }
                catch (InvalidOperationException error)
                {
                    Assert(error.Message.Contains("/LKP103") && error.Message.Contains("/LKP999") && error.Message.Contains("/LKP998"),
                        "Lookup did not identify every problematic route: " + error.Message);
                }
                Assert(before.Count == session.SegmentItems.Count && before.All(pair => session.SegmentItems[pair.Key].Equals(pair.Value)),
                    "An unsuccessful name lookup committed a partial geometry capture.");
            }
        });
        Check("lookup_cached_duplicate_and_parent_visibility_is_rechecked_after_hide_and_unhide", () =>
        {
            using (var session = new RoutingSession())
            {
                var duplicates = fixture.Route("/LKP103");
                using (var hidden = new HiddenSnapshot(document, fixture.All))
                {
                    ExpectInvalid(() => Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP103" }, CancellationToken.None)), "More than one");
                    document.Models.SetHidden(new[] { duplicates[1] }, true);
                    var single = Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP103" }, CancellationToken.None));
                    Assert(single.Count == 1 && session.SegmentItems.Values.Single().Ancestors.Contains(duplicates[0]),
                        "A hidden duplicate route still blocked the remaining visible route.");
                    document.Models.SetHidden(new[] { duplicates[0] }, true);
                    ExpectInvalidWords(() => Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP103" }, CancellationToken.None)), "visible", "hidden");
                    document.Models.SetHidden(duplicates, false);
                    ExpectInvalid(() => Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP103" }, CancellationToken.None)), "More than one");
                    var parent = fixture.Route("/LKP101").Single();
                    Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP101" }, CancellationToken.None));
                    document.Models.SetHidden(new[] { parent }, true);
                    ExpectInvalidWords(() => Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP101" }, CancellationToken.None)), "visible", "hidden");
                    document.Models.SetHidden(new[] { parent }, false);
                    Assert(Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP101" }, CancellationToken.None)).Count == 3,
                        "A cached route match stayed unavailable after unhiding its parent.");
                }
            }
        });
        Check("lookup_cancelled_capture_keeps_previous_native_snapshot", () =>
        {
            using (var session = new RoutingSession())
            using (var cancellation = new CancellationTokenSource())
            {
                Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP101" }, CancellationToken.None));
                var identities = session.SegmentItems.Values.ToArray();
                cancellation.Cancel();
                ExpectCancelled(() => Wait(session.CaptureLookupSegmentsAsync(new[] { "/LKP102" }, cancellation.Token)));
                Assert(new HashSet<ModelItem>(identities).SetEquals(session.SegmentItems.Values),
                    "Cancelled native lookup replaced its previous complete capture.");
            }
        });
        Check("route_only_visualization_selects_route_preserves_other_transparency_and_restores_prior_selection", () =>
        {
            var route = fixture.Route("/LKP101").Single().Descendants.Where(item => !item.Children.Any() && item.HasGeometry).ToArray();
            var background = fixture.Route("/LKP102").Single().Descendants.Single(item => !item.Children.Any() && item.HasGeometry);
            var prior = sampleLeaves[0];
            var snapshots = Snapshot(fixture.Leaves.Concat(new[] { prior }).ToList());
            var original = snapshots.Single(item => item.Item.Equals(background));
            try
            {
                document.Models.OverrideTemporaryTransparency(new[] { background }, 0.37);
                var before = Snapshot(fixture.Leaves.Concat(new[] { prior }).ToList());
                document.CurrentSelection.CopyFrom(new[] { prior });
                using (var visualization = new PathVisualization())
                {
                    visualization.ShowRoute(document, route);
                    Assert(visualization.IsShown && visualization.IsPathSelected, "Route-only visualization did not own the selected route.");
                    AssertSelection(route);
                    foreach (var leaf in route) { AssertColor(leaf, 0.10, 0.65, 1.00); AssertNear(leaf.Geometry.ActiveTransparency, 0); }
                    AssertAppearance(before.Where(item => !route.Contains(item.Item)).ToList(), true, true);
                    AssertNear(background.Geometry.ActiveTransparency, 0.37);
                    visualization.Restore(); AssertSelection(prior); AssertAppearance(before, true, true);
                    visualization.ShowRoute(document, route);
                    document.CurrentSelection.CopyFrom(new[] { background });
                    visualization.Restore(); AssertSelection(background);
                }
            }
            finally { RestoreLeafAppearance(original); }
            AssertAppearance(snapshots, true, true);
        });
        Check("lookup_tab_manual_input_shared_gap_and_busy_controls_have_correct_layout", () =>
        {
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700); control.CreateControl(); control.PerformLayout();
                var tabs = Field<TabControl>(control, "tabs");
                Assert(tabs.TabPages.Count == 3 && tabs.TabPages[2].Text == "Path lookup", "The third Path lookup tab is absent.");
                tabs.SelectedIndex = 2; control.PerformLayout(); System.Windows.Forms.Application.DoEvents();
                var input = Field<TextBox>(control, "lookupInput"); var result = Field<TextBox>(control, "lookupOutput");
                Assert(input.Multiline && !input.ReadOnly && result.Multiline && result.ReadOnly, "Lookup input/result controls have incorrect editing behavior.");
                Assert(!ControlsOf(tabs.TabPages[2]).OfType<NumericUpDown>().Any(), "Lookup added its own Connection gap value.");
                var session = Field<RoutingSession>(control, "lookupSession");
                int builds = session.GeometryBuildCount;
                input.Text = "/secondary/LKP100/LKP101/SECONDARY/LKP102/Secondary";
                Field<NumericUpDown>(control, "gap").Value = 0.25m;
                document.CurrentSelection.CopyFrom(new[] { sampleLeaves[0] });
                System.Windows.Forms.Application.DoEvents();
                Assert(session.GeometryBuildCount == builds && Field<RouteResult>(control, "lookupResult") == null
                    && !Field<bool>(control, "busy"), "Typing/selecting automatically started route lookup.");
                Invoke(control, "StartOperation", "Lookup busy state check.");
                Assert(!input.Enabled && !Field<Button>(control, "lookupShow").Enabled
                    && !Field<Button>(control, "lookupRestore").Enabled && Field<Button>(control, "lookupCancel").Enabled,
                    "Lookup actions can be changed during a busy operation or cancellation is unavailable.");
                Invoke(control, "FinishOperation");
                Assert(input.Enabled && Field<Button>(control, "lookupShow").Enabled && !Field<Button>(control, "lookupCancel").Enabled,
                    "Lookup controls did not recover after a completed operation.");
                SavePreview(control, "pathfinder-lookup-preview.png");
            }
        });
        Check("lookup_ui_uses_ordered_route_and_path_gap_draws_overlay_without_background_alpha_and_restores", () =>
        {
            var before = Snapshot(fixture.Leaves);
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700); control.CreateControl();
                Field<TabControl>(control, "tabs").SelectedIndex = 2;
                Field<TextBox>(control, "lookupInput").Text = "/secondary/LKP100/LKP101/SECONDARY/LKP102/Secondary";
                Field<NumericUpDown>(control, "gap").Value = 0.25m;
                document.CurrentSelection.CopyFrom(new[] { sampleLeaves[0] });
                var selectionBefore = document.CurrentSelection.SelectedItems.ToArray();
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                var calculated = Field<RouteResult>(control, "lookupResult");
                Assert(calculated != null && calculated.Success, "Ordered lookup failed: " + Field<TextBox>(control, "lookupOutput").Text);
                Assert(calculated.RouteText == "/LKP100/LKP101/LKP102", "Lookup reordered routes or retained SECONDARY markers: " + calculated.RouteText);
                var session = Field<RoutingSession>(control, "lookupSession");
                var selected = calculated.SegmentIds.Select(id => session.SegmentItems[id]).Distinct().ToArray();
                Assert(selected.Length == 4 && selected.All(item => item.DisplayName != "Tube LKP off branch"),
                    "Ordered lookup selected an unnecessary side branch or omitted a main-route leaf.");
                AssertSelection(selected); AssertOverlayPoints(calculated.PathPoints);
                Assert(RoutePathOverlay.IsShownFor(document) && Field<Button>(control, "lookupRestore").Enabled,
                    "Lookup did not show a restorable native yellow line.");
                AssertAppearance(before.Where(item => !selected.Contains(item.Item)).ToList(), true, true);
                SavePreview(control, "pathfinder-lookup-result-preview.png");
                Invoke(control, "RestoreView"); AssertSelection(selectionBefore); AssertAppearance(before, true, true);
                Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Lookup Restore left the yellow overlay.");
                Field<NumericUpDown>(control, "gap").Value = 0.01m;
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                calculated = Field<RouteResult>(control, "lookupResult");
                Assert(calculated == null || !calculated.Success, "Lookup ignored the current smaller Path Connection gap.");
                string error = Field<TextBox>(control, "lookupOutput").Text;
                Assert(error.Contains("/LKP101") && error.Contains("/LKP102"), "Disconnected lookup did not identify the route pair: " + error);
                Assert(RoutePathOverlay.DisplayedPoints.Count == 0 && !Field<PathVisualization>(control, "visualization").IsShown,
                    "Disconnected lookup displayed a partial successful path.");
                AssertSelection(selectionBefore);
            }
            AssertAppearance(before, true, true);
        });
        Check("lookup_ui_resolves_branched_first_and_last_routes_and_preserves_missing_name_errors", () =>
        {
            var before = Snapshot(fixture.Leaves);
            using (var control = new PathFinderControl())
            {
                var input = Field<TextBox>(control, "lookupInput");
                var result = Field<TextBox>(control, "lookupOutput");
                document.CurrentSelection.CopyFrom(new[] { sampleLeaves[0] });
                var selectionBefore = document.CurrentSelection.SelectedItems.ToArray();
                input.Text = "/LKP101/LKP102";
                Field<NumericUpDown>(control, "gap").Value = 0.25m;
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                var forward = Field<RouteResult>(control, "lookupResult");
                Assert(forward != null && forward.Success && forward.RouteText == "/LKP101/LKP102",
                    "A branching first route still requires exactly two ends: " + result.Text);
                AssertLookupMainLeaves(control, forward, 3);
                Assert(Math.Abs(forward.LengthMeters - 12.2) < 0.001, "Branched first route did not follow its main span.");
                AssertOverlayPoints(forward.PathPoints);
                Invoke(control, "RestoreView"); AssertSelection(selectionBefore); AssertAppearance(before, true, true);
                input.Text = "/LKP102/LKP101";
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                var reversed = Field<RouteResult>(control, "lookupResult");
                Assert(reversed != null && reversed.Success && reversed.RouteText == "/LKP102/LKP101",
                    "A branching last route still requires exactly two ends: " + result.Text);
                AssertLookupMainLeaves(control, reversed, 3);
                AssertPoint(reversed.PathPoints.First(), forward.PathPoints.Last());
                AssertPoint(reversed.PathPoints.Last(), forward.PathPoints.First());
                AssertOverlayPoints(reversed.PathPoints);
                Invoke(control, "RestoreView"); AssertSelection(selectionBefore); AssertAppearance(before, true, true);
                input.Text = "/LKP103/LKP999";
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                Assert(result.Text.Contains("/LKP103") && result.Text.Contains("/LKP999"),
                    "Lookup UI did not display both ambiguous and missing logical routes.");
                input.Text = "/Secondary/SECONDARY/secondary";
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                Assert(Field<RouteResult>(control, "lookupResult")?.Success != true && result.Text.Length > 0
                    && !Field<PathVisualization>(control, "visualization").IsShown && RoutePathOverlay.DisplayedPoints.Count == 0,
                    "SECONDARY-only input produced an empty successful or partial visualization.");
            }
            AssertAppearance(before, true, true);
        });
        Check("lookup_ui_single_branched_route_shows_longest_main_span_and_excludes_short_spur", () =>
        {
            var before = Snapshot(fixture.Leaves);
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700); control.CreateControl();
                Field<TabControl>(control, "tabs").SelectedIndex = 2;
                document.CurrentSelection.CopyFrom(new[] { sampleLeaves[0] });
                var selectionBefore = document.CurrentSelection.SelectedItems.ToArray();
                Field<TextBox>(control, "lookupInput").Text = "/Secondary/LKP101/SECONDARY";
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                var calculated = Field<RouteResult>(control, "lookupResult");
                Assert(calculated != null && calculated.Success && calculated.RouteText == "/LKP101",
                    "A single branched route still requires exactly two ends: " + Field<TextBox>(control, "lookupOutput").Text);
                AssertLookupMainLeaves(control, calculated, 2);
                AssertNear(calculated.LengthMeters, 8);
                AssertOverlayPoints(calculated.PathPoints);
                Assert(RoutePathOverlay.IsShownFor(document), "Single branched route did not show the yellow line.");
                SavePreview(control, "pathfinder-lookup-single-branch-preview.png");
                Invoke(control, "RestoreView"); AssertSelection(selectionBefore); AssertAppearance(before, true, true);
                Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Single branched route Restore retained its yellow line.");
            }
            AssertAppearance(before, true, true);
        });
        Check("lookup_and_path_keep_separate_capture_identity_maps_and_path_can_be_shown_again", () =>
        {
            using (var control = new PathFinderControl())
            {
                var first = fixture.Route("/LKP101").Single().Descendants.First(item => item.HasGeometry && !item.Children.Any());
                var last = fixture.Route("/LKP102").Single().Descendants.Single(item => item.HasGeometry && !item.Children.Any());
                document.CurrentSelection.CopyFrom(new[] { first }); Wait((Task)Invoke(control, "AssignAsync"));
                document.CurrentSelection.CopyFrom(new[] { first }); Invoke(control, "Pick", true);
                document.CurrentSelection.CopyFrom(new[] { last }); Invoke(control, "Pick", false);
                Wait((Task)Invoke(control, "CalculateAsync"));
                var calculated = Field<RouteResult>(control, "result");
                Assert(calculated != null && calculated.Success, "Path coexistence fixture failed to calculate.");
                var session = Field<RoutingSession>(control, "session");
                var nativeMap = session.SegmentItems.ToDictionary(pair => pair.Key, pair => pair.Value);
                int builds = session.GeometryBuildCount;
                Invoke(control, "ShowPath");
                Field<TextBox>(control, "lookupInput").Text = "/LKP100/LKP101/LKP102";
                Field<NumericUpDown>(control, "gap").Value = 0.25m;
                Wait((Task)Invoke(control, "ShowLookupAsync"));
                Assert(Field<RouteResult>(control, "lookupResult")?.Success == true, "Coexistence lookup failed.");
                Assert(ReferenceEquals(calculated, Field<RouteResult>(control, "result")) && session.GeometryBuildCount == builds
                    && nativeMap.Count == session.SegmentItems.Count && nativeMap.All(pair => session.SegmentItems[pair.Key].Equals(pair.Value)),
                    "Lookup overwrote the Path result or its native capture identity map.");
                Invoke(control, "ShowPath");
                AssertSelection(calculated.SegmentIds.Select(id => nativeMap[id]).ToArray());
                AssertOverlayPoints(calculated.PathPoints);
                Invoke(control, "RestoreView");
            }
        });
        Check("lookup_cancel_and_dispose_pending_native_search_leave_no_view_or_selection_effect", () =>
        {
            var selectionBefore = document.CurrentSelection.SelectedItems.ToArray();
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700); control.CreateControl();
                Field<TabControl>(control, "tabs").SelectedIndex = 2;
                control.PerformLayout(); System.Windows.Forms.Application.DoEvents();
                Field<TextBox>(control, "lookupInput").Text = "/LKP100/LKP101/LKP102";
                var task = (Task)Invoke(control, "ShowLookupAsync");
                Assert(Field<bool>(control, "busy"), "Lookup did not expose its pending native capture state.");
                Field<Button>(control, "lookupCancel").PerformClick(); Wait(task);
                Assert(!Field<bool>(control, "busy") && Field<RouteResult>(control, "lookupResult") == null
                    && !Field<PathVisualization>(control, "visualization").IsShown && RoutePathOverlay.DisplayedPoints.Count == 0,
                    "Cancelled lookup retained a partial result or visualization: " + Field<TextBox>(control, "lookupOutput").Text);
                AssertSelection(selectionBefore);
            }
            var disposed = new PathFinderControl();
            Field<TextBox>(disposed, "lookupInput").Text = "/LKP100/LKP101/LKP102";
            var pending = (Task)Invoke(disposed, "ShowLookupAsync");
            disposed.Dispose(); Wait(pending);
            Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Disposed pending lookup left a yellow overlay.");
            AssertSelection(selectionBefore);
        });
    }

    private void AssertLookupMainLeaves(PathFinderControl control, RouteResult result, int expectedCount)
    {
        var session = Field<RoutingSession>(control, "lookupSession");
        var selected = result.SegmentIds.Select(id => session.SegmentItems[id]).Distinct().ToArray();
        Assert(selected.Length == expectedCount && selected.All(item => !item.AncestorsAndSelf.Any(owner => owner.DisplayName == "Tube LKP off branch")),
            "Lookup included an unnecessary branch or omitted a main-route leaf.");
        AssertSelection(selected);
    }

    private sealed class LookupFixture
    {
        public readonly List<ModelItem> All;
        public readonly List<ModelItem> Leaves;
        public LookupFixture(ModelItem root)
        {
            All = root.DescendantsAndSelf.ToList();
            Leaves = All.Where(item => item.HasGeometry && !item.Children.Any()).ToList();
        }
        public List<ModelItem> Route(string name)
        {
            var matches = new HashSet<ModelItem>(All.Where(item => RouteCodeParser.Parse(item.DisplayName) == name));
            return matches.Where(item => !item.Ancestors.Any(matches.Contains)).ToList();
        }
    }

    private static string CreateLookupIfc()
    {
        var writer = new StringBuilder();
        writer.AppendLine("ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(('Public deterministic PathFinder native fixture'),'2;1');\nFILE_NAME('path-lookup-fixture.ifc','2026-10-12T00:00:00',('PathFinder'),('PathFinder'),'PathFinder smoke','PathFinder','');\nFILE_SCHEMA(('IFC2X3'));\nENDSEC;\nDATA;");
        long next = 0;
        Func<string, long> entity = value => { long id = ++next; writer.AppendLine("#" + id + "=" + value + ";"); return id; };
        Func<long, string> reference = id => "#" + id;
        Func<string> guid = () => "'0" + (next + 1).ToString("D21", CultureInfo.InvariantCulture) + "'";
        Func<double, string> number = value =>
        {
            string text = value.ToString("R", CultureInfo.InvariantCulture);
            return text.Contains(".") || text.Contains("E") ? text : text + ".";
        };
        long person = entity("IFCPERSON($,$,'PathFinder',$,$,$,$,$)");
        long organization = entity("IFCORGANIZATION($,'PathFinder',$,$,$)");
        long actor = entity("IFCPERSONANDORGANIZATION(" + reference(person) + "," + reference(organization) + ",$)");
        long application = entity("IFCAPPLICATION(" + reference(organization) + ",'1.0','PathFinder smoke','PathFinder')");
        long history = entity("IFCOWNERHISTORY(" + reference(actor) + "," + reference(application) + ",$,.ADDED.,$,$,$,0)");
        long origin = entity("IFCCARTESIANPOINT((0.,0.,0.))");
        long axis = entity("IFCAXIS2PLACEMENT3D(" + reference(origin) + ",$,$)");
        long placement = entity("IFCLOCALPLACEMENT($," + reference(axis) + ")");
        long up = entity("IFCDIRECTION((0.,0.,1.))");
        long origin2 = entity("IFCCARTESIANPOINT((0.,0.))");
        long axis2 = entity("IFCAXIS2PLACEMENT2D(" + reference(origin2) + ",$)");
        long context = entity("IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,0.00001," + reference(axis) + ",$)");
        long unit = entity("IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.)");
        long units = entity("IFCUNITASSIGNMENT((" + reference(unit) + "))");
        long project = entity("IFCPROJECT(" + guid() + "," + reference(history) + ",'Path lookup fixture',$,$,$,$,(" + reference(context) + ")," + reference(units) + ")");
        long building = entity("IFCBUILDING(" + guid() + "," + reference(history) + ",'Fixture building',$,$," + reference(placement) + ",$,$,.ELEMENT.,$,$,$)");
        var children = new Dictionary<long, List<long>>();
        children.Add(project, new List<long> { building }); children.Add(building, new List<long>());
        Func<string, long, long> parent = (name, owner) =>
        {
            long id = entity("IFCELEMENTASSEMBLY(" + guid() + "," + reference(history) + ",'" + name + "',$,$," + reference(placement) + ",$,$,.NOTDEFINED.,.NOTDEFINED.)");
            children[owner].Add(id); children.Add(id, new List<long>()); return id;
        };
        Action<string, long, double, double, double, double> mesh = (name, owner, x0, x1, y0, y1) =>
        {
            long point = entity("IFCCARTESIANPOINT((" + number((x0 + x1) * 0.5) + "," + number((y0 + y1) * 0.5) + ",1.9))");
            long positionedAxis = entity("IFCAXIS2PLACEMENT3D(" + reference(point) + ",$,$)");
            long localPlacement = entity("IFCLOCALPLACEMENT($," + reference(positionedAxis) + ")");
            long profile = entity("IFCRECTANGLEPROFILEDEF(.AREA.,$," + reference(axis2) + "," + number(x1 - x0) + "," + number(y1 - y0) + ")");
            long solid = entity("IFCEXTRUDEDAREASOLID(" + reference(profile) + "," + reference(axis) + "," + reference(up) + ",0.2)");
            long shape = entity("IFCSHAPEREPRESENTATION(" + reference(context) + ",'Body','SweptSolid',(" + reference(solid) + "))");
            long representation = entity("IFCPRODUCTDEFINITIONSHAPE($,$,(" + reference(shape) + "))");
            long id = entity("IFCBUILDINGELEMENTPROXY(" + guid() + "," + reference(history) + ",'" + name + "',$,$," + reference(localPlacement) + "," + reference(representation) + ",$,.ELEMENT.)");
            children[owner].Add(id);
        };
        long zone = parent("/PATH_LOOKUP_FIXTURE", building);
        long incoming = parent("/LKP100", zone);
        mesh("Tube LKP incoming", incoming, 9996, 10000, -0.1, 0.1);
        long first = parent("/LKP101", zone);
        mesh("Tube LKP main 1", first, 10000, 10004, -0.1, 0.1);
        mesh("Tube LKP main 2", first, 10004, 10008, -0.1, 0.1);
        mesh("Tube LKP off branch", first, 10003.9, 10004.1, 0, 0.5);
        long second = parent("/LKP102", zone);
        mesh("Tube LKP continuation", second, 10008.2, 10012.2, -0.1, 0.1);
        long logical = parent("/5LD04", zone);
        long qualified = parent("BRANCH /5LD04/B1", logical);
        mesh("Tube /B1", qualified, 20000, 20002, -0.1, 0.1);
        mesh("Tube /B2", logical, 20002, 20004, -0.1, 0.1);
        long duplicateFirst = parent("/LKP103", zone);
        mesh("Tube duplicate 1", duplicateFirst, 30000, 30002, -0.1, 0.1);
        long duplicateSecond = parent("/LKP103", zone);
        mesh("Tube duplicate 2", duplicateSecond, 30010, 30012, -0.1, 0.1);
        long disconnected = parent("/LKP104", zone);
        mesh("Tube disconnected", disconnected, 40000, 40002, -0.1, 0.1);
        long generic = parent("/LKP106", zone);
        mesh("Elbow named sample", generic, 50000, 50002, -0.1, 0.1);
        foreach (var pair in children)
            if (pair.Value.Count > 0) entity("IFCRELAGGREGATES(" + guid() + "," + reference(history) + ",$,$," + reference(pair.Key) + ",(" + string.Join(",", pair.Value.Select(reference)) + "))");
        writer.AppendLine("ENDSEC;\nEND-ISO-10303-21;");
        return writer.ToString();
    }
}
