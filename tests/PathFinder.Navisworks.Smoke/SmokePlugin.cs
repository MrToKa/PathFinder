using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using AddinRibbon.Ctr;
using AddinRibbon.Routing;
using AddinRibbon.Services;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NApp = Autodesk.Navisworks.Api.Application;

[Plugin("PathFinderSmoke", "TEST", DisplayName = "Isolated PathFinder smoke")]
[AddInPlugin(AddInLocation.None)]
public sealed class PathFinderSmoke : AddInPlugin
{
    private readonly List<Dictionary<string, object>> checks = new List<Dictionary<string, object>>();
    private string output;
    private Document document;
    private List<ModelItem> geometry;
    private bool modifiedBeforeVisualization, modifiedAfterVisualization, modifiedAfterRestore;

    public override int Execute(params string[] parameters)
    {
        output = parameters[0];
        var previousContext = SynchronizationContext.Current;
        if (previousContext == null) SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        try
        {
            document = NApp.ActiveDocument;
            geometry = document.Models.CreateCollectionFromRootItems().DescendantsAndSelf.Where(item => item.HasGeometry).ToList();
            if (parameters.Length > 1 && parameters[1] == "extract-model")
            {
                ExtractModel();
                return 0;
            }
            var leaves = geometry.Where(item => !item.Children.Any() && !item.BoundingBox().IsEmpty).ToList();
            if (leaves.Count < 5) throw new InvalidOperationException("Smoke sample must have at least five geometry leaves.");
            TestVisualization(leaves);
            TestRoutingSession(leaves);
            TestControl();
            TestControlFlow(leaves);
        }
        catch (Exception error)
        {
            checks.Add(Result("fatal", false, error.ToString()));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            if (!(parameters.Length > 1 && parameters[1] == "extract-model" && checks.Count == 0)) Write();
        }
        return checks.Any(check => !(bool)check["passed"]) ? 1 : 0;
    }

    private void ExtractModel()
    {
        bool before = document.IsModified;
        const int recordCap = 5000;
        double scale = UnitConversion.ScaleFactor(document.Units, Units.Meters);
        var timer = Stopwatch.StartNew();
        var namedAncestors = new Dictionary<ModelItem, RouteMarker>();
        var routeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<Dictionary<string, object>>();
        int visited = 0, leaves = 0, namedLeaves = 0, emptyBounds = 0;
        WriteInspectionProgress("reading deepest geometry", visited, leaves, namedLeaves, rows.Count, recordCap, timer);
        foreach (var leaf in geometry)
        {
            if (++visited % 500 == 0) WriteInspectionProgress("reading deepest geometry", visited, leaves, namedLeaves, rows.Count, recordCap, timer);
            if (leaf.Children.Any()) continue;
            leaves++;
            var closest = ClosestRoute(leaf, namedAncestors);
            if (closest == null) continue;
            namedLeaves++; routeCodes.Add(closest.Code);
            if (rows.Count >= recordCap) continue;
            var box = leaf.BoundingBox(); if (box.IsEmpty) { emptyBounds++; continue; }
            rows.Add(new Dictionary<string, object>
            {
                { "leafName", leaf.DisplayName }, { "routeName", closest.Name }, { "routeCode", closest.Code },
                { "worldMinMeters", new[] { box.Min.X * scale, box.Min.Y * scale, box.Min.Z * scale } },
                { "worldMaxMeters", new[] { box.Max.X * scale, box.Max.Y * scale, box.Max.Z * scale } }
            });
        }
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(output, serializer.Serialize(new Dictionary<string, object>
        {
            { "apiAssembly", typeof(Document).Assembly.FullName }, { "document", document.FileName }, { "documentUnits", document.Units.ToString() },
            { "metresPerDocumentUnit", scale }, { "geometryItemCount", geometry.Count }, { "geometryLeafCount", leaves },
            { "routeNamedLeafCount", namedLeaves }, { "inspectedRecordCount", rows.Count }, { "recordCap", recordCap },
            { "truncated", namedLeaves > rows.Count + emptyBounds }, { "omittedNamedLeafCount", namedLeaves - rows.Count },
            { "emptyBoundsAmongInspectedLeaves", emptyBounds }, { "uniqueRouteCodes", routeCodes.OrderBy(code => code).ToArray() },
            { "documentModifiedBefore", before }, { "documentModifiedAfter", document.IsModified }, { "routeLeaves", rows }
        }));
        WriteInspectionProgress("complete", visited, leaves, namedLeaves, rows.Count, recordCap, timer);
    }

    private RouteMarker ClosestRoute(ModelItem leaf, Dictionary<ModelItem, RouteMarker> cache)
    {
        var trail = new List<ModelItem>();
        var current = leaf; RouteMarker result = null;
        while (current != null)
        {
            if (cache.TryGetValue(current, out result)) break;
            trail.Add(current);
            var name = current.DisplayName;
            var code = NamedCode(name);
            if (code != null) { result = new RouteMarker { Name = name, Code = code }; break; }
            current = current.Parent;
        }
        foreach (var item in trail) cache[item] = result;
        return result;
    }

    private void WriteInspectionProgress(string stage, int visited, int leaves, int namedLeaves, int records, int cap, Stopwatch timer)
    {
        File.WriteAllText(output + ".progress.json", new JavaScriptSerializer().Serialize(new Dictionary<string, object>
        {
            { "stage", stage }, { "geometryItemCount", geometry.Count }, { "visitedGeometryItems", visited }, { "geometryLeafCountSoFar", leaves },
            { "routeNamedLeafCountSoFar", namedLeaves }, { "inspectedRecordCount", records }, { "recordCap", cap }, { "elapsedSeconds", timer.Elapsed.TotalSeconds }
        }));
    }
    private sealed class RouteMarker { public string Name; public string Code; }
    private static string NamedCode(string name)
    {
        string code;
        return RouteCodeParser.TryParseNamedCode(name, out code) ? code.TrimStart('/') : null;
    }

    private void TestVisualization(List<ModelItem> leaves)
    {
        var from = leaves[0]; var to = leaves[1]; var path = leaves[2]; var background = leaves[3];
        using (var visualization = new PathVisualization())
        {
            var clean = Snapshot(geometry);
            modifiedBeforeVisualization = document.IsModified;
            Check("temporary_materials_preserve_permanent_values_and_restore_view", () =>
            {
                try
                {
                    visualization.Show(document, new[] { path }, from, to);
                    modifiedAfterVisualization = document.IsModified;
                    AssertAppearance(clean, false, true);
                }
                finally { visualization.Restore(); }
                modifiedAfterRestore = document.IsModified;
                AssertAppearance(clean, true, true);
            });

            using (var color = new Color(0.71, 0.32, 0.16)) document.Models.OverridePermanentColor(new[] { background }, color);
            document.Models.OverridePermanentTransparency(new[] { background }, 0.37);
            using (var color = new Color(0.33, 0.44, 0.77)) document.Models.OverrideTemporaryColor(new[] { path }, color);
            document.Models.OverrideTemporaryTransparency(new[] { path }, 0.25);
            document.Models.SetHidden(new[] { background }, true);
            var before = Snapshot(geometry);
            Check("show_path_opaque_colored_endpoints_and_route", () =>
            {
                visualization.Show(document, new[] { path }, from, to);
                AssertColor(from, 0.10, 0.85, 0.25); AssertColor(to, 1.00, 0.45, 0.05); AssertColor(path, 0.10, 0.65, 1.00);
                AssertNear(from.Geometry.ActiveTransparency, 0); AssertNear(to.Geometry.ActiveTransparency, 0); AssertNear(path.Geometry.ActiveTransparency, 0);
                foreach (var item in geometry.Where(item => !item.Equals(from) && !item.Equals(to) && !item.Equals(path)))
                    AssertNear(item.Geometry.ActiveTransparency, 0.95);
                AssertAppearance(before, false, true);
                Assert(background.IsHidden, "Show path unhid the original hidden item.");
            });
            Check("restore_existing_permanent_and_temporary_appearance", () =>
            {
                visualization.Restore(); AssertAppearance(before, true, true);
                Assert(background.IsHidden, "Restore changed hidden state.");
            });
            Check("repeated_show_then_reverse_preserves_original_materials", () =>
            {
                visualization.Show(document, new[] { path }, from, to);
                visualization.Show(document, new[] { path }, to, from);
                AssertColor(to, 0.10, 0.85, 0.25); AssertColor(from, 1.00, 0.45, 0.05);
                visualization.Restore(); AssertAppearance(before, true, true);
            });
            Check("same_from_to_object_is_opaque_and_purple", () =>
            {
                visualization.Show(document, new[] { path }, from, from);
                AssertColor(from, 0.75, 0.20, 1.00); AssertNear(from.Geometry.ActiveTransparency, 0);
                visualization.Restore(); AssertAppearance(before, true, true);
            });
            Check("dispose_restores_view", () =>
            {
                using (var disposable = new PathVisualization()) disposable.Show(document, new[] { path }, from, to);
                AssertAppearance(before, true, true);
            });
            document.Models.SetHidden(new[] { background }, false);
        }
    }

    private void TestRoutingSession(List<ModelItem> leaves)
    {
        using (var session = new RoutingSession())
        {
            var leaf = leaves[0];
            Check("selected_leaf_includes_itself", () =>
            {
                document.CurrentSelection.CopyFrom(new[] { leaf });
                int captured = Wait(session.AssignSelectionAsync(CableCategory.LV, CancellationToken.None));
                Assert(captured == 1 && session.Assignments.Count == 1 && session.Assignments[0].LeafCount == 1, "Selected leaf was omitted.");
                var segments = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(segments.Count == 1 && session.SegmentItems[segments[0].Id].Equals(leaf), "Wrong native leaf captured.");
            });
            var nested = leaf.Ancestors.FirstOrDefault(item => item.Descendants.Where(child => !child.Children.Any() && child.HasGeometry && !child.BoundingBox().IsEmpty).Take(2).Count() == 2);
            Check("nested_rule_expands_deepest_geometry_children", () =>
            {
                Assert(nested != null, "Sample had no nested root with two leaves.");
                var expected = nested.DescendantsAndSelf.Count(item => !item.Children.Any() && item.HasGeometry && !item.BoundingBox().IsEmpty);
                document.CurrentSelection.CopyFrom(new[] { nested });
                Assert(Wait(session.AssignSelectionAsync(CableCategory.Control, CancellationToken.None)) == expected, "Nested leaf count differs.");
                var segments = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(segments.Count == expected, "Overlap leaves were double counted.");
                Assert(segments.All(segment => segment.AllowedCategories == CableCategory.Control), "Latest parent rule did not win.");
            });
            Check("overlapping_leaf_rule_last_assignment_wins", () =>
            {
                document.CurrentSelection.CopyFrom(new[] { leaf });
                Wait(session.AssignSelectionAsync(CableCategory.MV, CancellationToken.None));
                var segments = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                var selected = segments.Single(segment => session.SegmentItems[segment.Id].Equals(leaf));
                Assert(selected.AllowedCategories == CableCategory.MV, "Latest leaf rule did not win.");
                Assert(segments.Where(segment => !session.SegmentItems[segment.Id].Equals(leaf)).All(segment => segment.AllowedCategories == CableCategory.Control), "Leaf override changed siblings.");
            });
            Check("geometry_coordinates_convert_document_units_to_meters", () =>
            {
                var raw = leaf.BoundingBox().Center;
                double scale = UnitConversion.ScaleFactor(document.Units, Units.Meters);
                var metres = session.CenterInMeters(leaf);
                AssertNear(metres.X, raw.X * scale); AssertNear(metres.Y, raw.Y * scale); AssertNear(metres.Z, raw.Z * scale);
                AssertNear(UnitConversion.ScaleFactor(Units.Millimeters, Units.Meters), 0.001);
                AssertNear(UnitConversion.ScaleFactor(Units.Feet, Units.Meters), 0.3048);
            });
            Check("item_transform_fingerprint_detects_stale_captured_path", () =>
            {
                var fingerprint = typeof(RoutingSession).GetMethod("AreCapturedSegmentsCurrent");
                Assert(fingerprint != null, "Captured geometry fingerprint method is missing.");
                Assert((bool)fingerprint.Invoke(session, null), "Fresh captures unexpectedly stale.");
                using (var translation = Transform3D.CreateTranslation(new Vector3D(0.125, 0, 0)))
                    document.Models.OverridePermanentTransform(new[] { leaf }, translation, true);
                Assert(!(bool)fingerprint.Invoke(session, null), "Changed item bounding box was accepted as current.");
                document.Models.ResetPermanentTransform(new[] { leaf });
                Assert((bool)fingerprint.Invoke(session, null), "Reset item geometry did not match captured box.");
            });
            Check("missing_object_name_has_readable_error", () => ExpectInvalid(() => Wait(session.ResolveAsync("NO_SUCH_OBJECT_PF_SMOKE_928371", null, CancellationToken.None)), "Object not found"));
            var duplicate = document.Models.CreateCollectionFromRootItems().DescendantsAndSelf
                .Where(item => !string.IsNullOrWhiteSpace(item.DisplayName)).GroupBy(item => item.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            Check("duplicate_name_requires_picked_object", () =>
            {
                Assert(duplicate != null, "Sample has no duplicate display names.");
                ExpectInvalid(() => Wait(session.ResolveAsync(duplicate.Key, null, CancellationToken.None)), "More than one object");
                var picked = duplicate.First();
                Assert(Wait(session.ResolveAsync(duplicate.Key, picked, CancellationToken.None)).Equals(picked), "Picked duplicate was not resolved.");
            });
            Check("cancelled_capture_does_not_commit_partial_rules", () =>
            {
                int revision = session.Revision, count = session.Assignments.Count;
                var source = new CancellationTokenSource(); source.Cancel();
                try { Wait(session.AssignSelectionAsync(CableCategory.LV, source.Token)); throw new Exception("Cancelled operation completed."); }
                catch (OperationCanceledException) { }
                Assert(revision == session.Revision && count == session.Assignments.Count, "Cancellation changed assignments.");
            });
            Check("model_transform_event_invalidates_rules_and_snapshots", () =>
            {
                int revision = session.Revision;
                var model = document.Models[0];
                using (var translation = Transform3D.CreateTranslation(new Vector3D(0.001, 0, 0)))
                    document.Models.SetModelUnitsAndTransform(model, model.Units, translation, true);
                System.Windows.Forms.Application.DoEvents();
                Assert(session.Revision > revision && session.Assignments.Count == 0 && session.SegmentItems.Count == 0, "Transform did not invalidate route state.");
            });
        }
    }

    private void TestControl()
    {
        Check("two_tabs_and_pause_default_allow_manual_calculation", () =>
        {
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700);
                control.CreateControl();
                control.PerformLayout();
                var descendants = ControlsOf(control).ToList();
                var tabs = descendants.OfType<TabControl>().Single();
                Assert(tabs.TabPages.Count == 2 && tabs.TabPages[0].Text == "Routes" && tabs.TabPages[1].Text == "Path", "Expected Routes and Path tabs.");
                Assert(descendants.OfType<CheckBox>().Single(item => item.Text == "Pause automatic calculation").Checked, "Pause is not checked.");
                Assert(descendants.OfType<Button>().Single(item => item.Text == "Calculate path").Enabled, "Manual Calculate path disabled by Pause.");
                Assert((string)descendants.OfType<ComboBox>().Single().SelectedItem == "LV", "Cable type default is not LV.");
                var timer = (System.Windows.Forms.Timer)typeof(PathFinderControl).GetField("debounce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(control);
                Assert(!timer.Enabled, "Automatic calculation timer running by default.");
                SavePreview(control, "pathfinder-routes-preview.png");
                tabs.SelectedIndex = 1;
                control.PerformLayout();
                System.Windows.Forms.Application.DoEvents();
                SavePreview(control, "pathfinder-path-preview.png");
                var session = (RoutingSession)typeof(PathFinderControl).GetField("session", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(control);
                int revision = session.Revision;
                control.Dispose();
                var model = document.Models[0];
                using (var translation = Transform3D.CreateTranslation(new Vector3D(0.001, 0, 0)))
                    document.Models.SetModelUnitsAndTransform(model, model.Units, translation, true);
                System.Windows.Forms.Application.DoEvents();
                Assert(session.Revision == revision, "Disposed control still receives document model events.");
            }
        });
        Check("dock_plugin_registered_loads_and_disposes_its_control", () =>
        {
            var record = NApp.Plugins.FindPlugin("ClDockPanelUpdate.CONN");
            Assert(record is DockPanePluginRecord && record.IsEnabled, "Dock plugin was not registered/enabled.");
            var dock = (DockPanePlugin)(record.LoadedPlugin ?? record.LoadPlugin());
            var pane = dock.CreateControlPane();
            Assert(pane is PathFinderControl, "Dock created the wrong control.");
            dock.DestroyControlPane(pane);
            Assert(pane.IsDisposed, "DestroyControlPane did not dispose its control.");
        });
    }

    private void SavePreview(Control control, string name)
    {
        using (var image = new System.Drawing.Bitmap(control.Width, control.Height))
        {
            control.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, control.Width, control.Height));
            image.Save(Path.Combine(Path.GetDirectoryName(output), name), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private void TestControlFlow(List<ModelItem> leaves)
    {
        Check("manual_ui_handlers_assign_pick_calculate_show_reverse_restore", () =>
        {
            var before = Snapshot(geometry);
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700);
                control.CreateControl();
                document.CurrentSelection.CopyFrom(new[] { leaves[0] });
                Wait((Task)Invoke(control, "AssignAsync"));
                var session = Field<RoutingSession>(control, "session");
                Assert(session.Assignments.Count == 1 && session.Assignments[0].LeafCount == 1, "UI did not capture selected route leaf.");
                document.CurrentSelection.CopyFrom(new[] { leaves[0] }); Invoke(control, "Pick", true);
                document.CurrentSelection.CopyFrom(new[] { leaves[1] }); Invoke(control, "Pick", false);
                var pause = Field<CheckBox>(control, "pause");
                Assert(pause.Checked, "Manual UI flow unexpectedly unpaused automatic calculation.");
                Wait((Task)Invoke(control, "CalculateAsync"));
                var result = Field<RouteResult>(control, "result");
                Assert(result != null && result.Success, "Manual calculation failed while Pause checked: " + Field<TextBox>(control, "output").Text);
                Assert(Field<Button>(control, "show").Enabled, "Show path was not enabled after successful calculation.");
                Invoke(control, "ShowPath");
                var visualization = Field<PathVisualization>(control, "visualization");
                Assert(visualization.IsShown, "Show path handler did not apply visualization.");
                AssertNear(leaves[0].Geometry.ActiveTransparency, 0); AssertNear(leaves[1].Geometry.ActiveTransparency, 0);
                Invoke(control, "ReversePath");
                Assert(Field<ModelItem>(control, "resolvedFrom").Equals(leaves[1]) && Field<ModelItem>(control, "resolvedTo").Equals(leaves[0]), "Reverse did not swap resolved objects.");
                AssertColor(leaves[1], 0.10, 0.85, 0.25); AssertColor(leaves[0], 1.00, 0.45, 0.05);
                Invoke(control, "RestoreView"); Assert(!visualization.IsShown, "Restore handler left visualization active.");
                AssertAppearance(before, true, true);
                Field<TextBox>(control, "from").Text = "PF_SMOKE_MISSING_EDIT";
                Assert(Field<RouteResult>(control, "result") == null, "Changing an endpoint kept the old result.");
                Assert(!Field<System.Windows.Forms.Timer>(control, "debounce").Enabled, "Paused endpoint edit started an automatic calculation.");
                Assert(Field<Button>(control, "calculate").Enabled, "Pause disabled manual Calculate path after editing.");
            }
            AssertAppearance(before, true, true);
        });
    }

    private static T Field<T>(object target, string name)
    { return (T)target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target); }
    private static object Invoke(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, args); }

    private static IEnumerable<Control> ControlsOf(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls) foreach (var item in ControlsOf(child)) yield return item;
    }
    private static T Wait<T>(Task<T> task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Async host smoke step exceeded 60 seconds.");
            System.Windows.Forms.Application.DoEvents(); Thread.Sleep(1);
        }
        return task.GetAwaiter().GetResult();
    }
    private static void Wait(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Async UI smoke step exceeded 60 seconds.");
            System.Windows.Forms.Application.DoEvents(); Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult();
    }
    private static void ExpectInvalid(Action action, string message)
    {
        try { action(); } catch (InvalidOperationException error) { Assert(error.Message.Contains(message), error.Message); return; }
        throw new Exception("Expected InvalidOperationException containing: " + message);
    }
    private void Check(string name, Action action)
    {
        try { action(); checks.Add(Result(name, true, "Passed in actual Manage host.")); }
        catch (Exception error) { checks.Add(Result(name, false, error.ToString())); }
        Write();
    }
    private void Write()
    {
        File.WriteAllText(output, new JavaScriptSerializer().Serialize(new Dictionary<string, object>
        {
            { "apiAssembly", typeof(Document).Assembly.FullName }, { "document", document == null ? "" : document.FileName },
            { "documentUnits", document == null ? "" : document.Units.ToString() },
            { "documentModifiedBeforeVisualization", modifiedBeforeVisualization },
            { "documentModifiedAfterVisualization", modifiedAfterVisualization },
            { "documentModifiedAfterRestore", modifiedAfterRestore },
            { "geometryItems", geometry == null ? 0 : geometry.Count }, { "checks", checks }
        }));
    }
    private static Dictionary<string, object> Result(string name, bool passed, string detail)
    { return new Dictionary<string, object> { { "name", name }, { "passed", passed }, { "detail", detail } }; }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void AssertNear(double actual, double expected) { Assert(Math.Abs(actual - expected) < 0.000001, "Expected " + expected + ", got " + actual); }
    private static void AssertColor(ModelItem item, double r, double g, double b)
    { var c = item.Geometry.ActiveColor; AssertNear(c.R, r); AssertNear(c.G, g); AssertNear(c.B, b); }
    private static List<Appearance> Snapshot(IEnumerable<ModelItem> items)
    {
        return items.Select(item => new Appearance { Item = item,
            ActiveColor = Values(item.Geometry.ActiveColor), PermanentColor = Values(item.Geometry.PermanentColor),
            ActiveTransparency = item.Geometry.ActiveTransparency, PermanentTransparency = item.Geometry.PermanentTransparency }).ToList();
    }
    private static double[] Values(Color color) { return new[] { color.R, color.G, color.B }; }
    private static void AssertAppearance(List<Appearance> snapshots, bool active, bool permanent)
    {
        foreach (var snapshot in snapshots)
        {
            var geometry = snapshot.Item.Geometry;
            if (active)
            {
                var color = Values(geometry.ActiveColor); for (int i = 0; i < 3; i++) AssertNear(color[i], snapshot.ActiveColor[i]);
                AssertNear(geometry.ActiveTransparency, snapshot.ActiveTransparency);
            }
            if (permanent)
            {
                var color = Values(geometry.PermanentColor); for (int i = 0; i < 3; i++) AssertNear(color[i], snapshot.PermanentColor[i]);
                AssertNear(geometry.PermanentTransparency, snapshot.PermanentTransparency);
            }
        }
    }
    private sealed class Appearance
    {
        public ModelItem Item; public double[] ActiveColor, PermanentColor;
        public double ActiveTransparency, PermanentTransparency;
    }
}
