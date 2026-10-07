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
            TestVisibleObjects(leaves);
            TestCableOverlay();
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
                .Where(item => !string.IsNullOrWhiteSpace(item.DisplayName) && RoutingSession.IsVisibleEndpoint(item)).GroupBy(item => item.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            Check("duplicate_name_requires_picked_object", () =>
            {
                Assert(duplicate != null, "Sample has no duplicate display names.");
                ExpectInvalid(() => Wait(session.ResolveAsync(duplicate.Key, null, CancellationToken.None)), "More than one visible object");
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

    private void TestVisibleObjects(List<ModelItem> leaves)
    {
        var all = document.Models.CreateCollectionFromRootItems().DescendantsAndSelf.ToList();
        var duplicate = FindIndependentVisibleDuplicate(all);
        Check("one_visible_duplicate_name_resolves_without_hidden_matches", () =>
        {
            Assert(duplicate != null, "Sample contains no independently hideable duplicate-name fixture.");
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                ExpectInvalid(() => Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)), "More than one visible object");
                document.Models.SetHidden(duplicate.Matches.Where(item => !item.Equals(duplicate.Keep)), true);
                Assert(EffectivelyVisible(duplicate.Keep), "Fixture hide also hid the intended visible duplicate.");
                Assert(Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)).Equals(duplicate.Keep), "Hidden duplicate matches still prevent resolving the visible object.");
            }
        });
        Check("all_duplicate_matches_hidden_produce_readable_error", () =>
        {
            Assert(duplicate != null, "Sample contains no independently hideable duplicate-name fixture.");
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                ExpectInvalid(() => Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)), "More than one visible object");
                document.Models.SetHidden(duplicate.Matches, true);
                ExpectInvalidWords(() => Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)), "hidden", "visible");
            }
        });
        Check("cached_name_lookup_refilters_after_hide_and_unhide", () =>
        {
            Assert(duplicate != null, "Sample contains no independently hideable duplicate-name fixture.");
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                ExpectInvalid(() => Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)), "More than one visible object");
                document.Models.SetHidden(duplicate.Matches.Where(item => !item.Equals(duplicate.Keep)), true);
                Assert(Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)).Equals(duplicate.Keep), "First cached visible lookup failed.");
                document.Models.SetHidden(new[] { duplicate.Keep }, true);
                ExpectInvalidWords(() => Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)), "hidden", "visible");
                document.Models.SetHidden(new[] { duplicate.Keep }, false);
                Assert(Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)).Equals(duplicate.Keep), "Unhidden object remained excluded by a cached name lookup.");
                document.Models.SetHidden(duplicate.Matches, false);
                ExpectInvalid(() => Wait(session.ResolveAsync(duplicate.Name, null, CancellationToken.None)), "More than one visible object");
            }
        });
        Check("hidden_picked_duplicate_cannot_bypass_visibility_filter", () =>
        {
            Assert(duplicate != null, "Sample contains no independently hideable duplicate-name fixture.");
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                document.Models.SetHidden(duplicate.Matches.Where(item => !item.Equals(duplicate.Keep)), true);
                var picked = duplicate.Matches.First(item => !item.Equals(duplicate.Keep));
                Assert(!EffectivelyVisible(picked), "Picked duplicate fixture is not hidden.");
                ExpectInvalidWords(() => Wait(session.ResolveAsync(duplicate.Name, picked, CancellationToken.None)), "hidden", "visible");
            }
        });
        Check("hidden_ancestor_excludes_named_child_and_route_descendants", () =>
        {
            var child = all.FirstOrDefault(item => item.Parent != null && !string.IsNullOrWhiteSpace(item.DisplayName) && RoutingSession.IsVisibleEndpoint(item));
            Assert(child != null, "Sample has no visible named child with an ancestor.");
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                document.Models.SetHidden(new[] { child.Parent }, true);
                Assert(!EffectivelyVisible(child), "Hiding the ancestor did not hide its child.");
                ExpectInvalidWords(() => Wait(session.ResolveAsync(child.DisplayName, child, CancellationToken.None)), "hidden", "visible");
                var root = leaves[0].Ancestors.First(item => item.DescendantsAndSelf.Any(candidate => !candidate.Children.Any() && candidate.HasGeometry));
                hidden.Restore();
                document.CurrentSelection.CopyFrom(new[] { root });
                Wait(session.AssignSelectionAsync(CableCategory.LV, CancellationToken.None));
                document.Models.SetHidden(new[] { root }, true);
                AssertNoVisibleCapturedRoutes(session);
                document.CurrentSelection.CopyFrom(new[] { root });
                Wait(session.AssignSelectionAsync(CableCategory.LV, CancellationToken.None));
                AssertNoVisibleCapturedRoutes(session);
            }
        });
        Check("hidden_route_leaf_is_skipped_and_unhide_restores_capture", () =>
        {
            var root = leaves[0].Ancestors.FirstOrDefault(item => item.DescendantsAndSelf.Count(candidate => !candidate.Children.Any() && candidate.HasGeometry && !candidate.BoundingBox().IsEmpty) > 1);
            Assert(root != null, "Sample has no route parent with multiple geometry leaves.");
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                document.CurrentSelection.CopyFrom(new[] { root });
                int assigned = Wait(session.AssignSelectionAsync(CableCategory.LV, CancellationToken.None));
                var original = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(original.Count == assigned && original.Count > 1, "Baseline route fixture capture failed.");
                ExpectInvalid(() => Wait(session.ResolveAsync("NO_SUCH_OBJECT_VISIBILITY_CACHE_643915", null, CancellationToken.None)), "Object not found");
                var nameIndex = Field<object>(session, "nameIndex");
                Assert(nameIndex != null, "Visibility event test did not populate the reusable name index.");
                int revision = session.Revision, assignmentCount = session.Assignments.Count;
                var leaf = session.SegmentItems[original[0].Id];
                document.Models.SetHidden(new[] { leaf }, true);
                System.Windows.Forms.Application.DoEvents();
                Assert(session.Revision > revision, "Native hidden-property event did not invalidate the routing revision.");
                Assert(session.Assignments.Count == assignmentCount, "Visibility change discarded route assignments.");
                Assert(ReferenceEquals(nameIndex, Field<object>(session, "nameIndex")), "Visibility change discarded the reusable name index.");
                Assert(!session.AreCapturedSegmentsCurrent(), "Hiding a captured tray did not make the cached path stale.");
                var expected = root.DescendantsAndSelf.Count(item => !item.Children.Any() && item.HasGeometry && !item.BoundingBox().IsEmpty && EffectivelyVisible(item));
                Assert(expected < original.Count, "Native hide did not exclude a route leaf.");
                var visible = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(visible.Count == expected && session.SegmentItems.Values.All(EffectivelyVisible), "Geometry capture included a hidden tray leaf.");
                hidden.Restore();
                var restored = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(restored.Count == original.Count, "Unhiding a tray did not restore geometry capture.");
                document.Models.SetHidden(new[] { leaf }, true);
                document.CurrentSelection.CopyFrom(new[] { root });
                Wait(session.AssignSelectionAsync(CableCategory.Control, CancellationToken.None));
                var recaptured = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(recaptured.Count == expected && session.SegmentItems.Values.All(EffectivelyVisible), "Capturing an added route parent included hidden deepest children.");
                hidden.Restore();
                Assert(Wait(session.CaptureSegmentsAsync(CancellationToken.None)).Count == original.Count, "Unhiding children of an added route parent did not restore the network.");
            }
        });
        Check("visible_parent_center_uses_only_remaining_visible_geometry_bounds", () =>
        {
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                double scale = UnitConversion.ScaleFactor(document.Units, Units.Meters);
                var boxes = new Dictionary<ModelItem, double[]>();
                foreach (var leaf in leaves.Where(EffectivelyVisible))
                    using (var box = leaf.BoundingBox())
                        boxes.Add(leaf, new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z });
                var parents = boxes.Keys.SelectMany(leaf => leaf.Ancestors.Select(parent => new { Parent = parent, Leaf = leaf }))
                    .GroupBy(pair => pair.Parent).Where(group => group.Count() > 1 && !group.Key.HasGeometry)
                    .OrderBy(group => group.Count()).Take(100);
                bool verified = false;
                int probes = 0;
                foreach (var group in parents)
                {
                    var parent = group.Key;
                    // This fixture must have all geometry at deepest leaves, so
                    // the independent union includes every drawable shape.
                    if (parent.Descendants.Any(item => item.HasGeometry && item.Children.Any())) continue;
                    var children = group.Select(pair => pair.Leaf).ToList();
                    var originalCenter = CenterOfLeafUnion(children, boxes, scale);
                    var candidates = children.OrderByDescending(leaf => CenterOfLeafUnion(new[] { leaf }, boxes, scale).DistanceTo(originalCenter)).Take(8);
                    foreach (var keep in candidates)
                    {
                        if (CenterOfLeafUnion(new[] { keep }, boxes, scale).DistanceTo(originalCenter) <= 0.000001) continue;
                        if (++probes > 32) break;
                        hidden.Restore();
                        document.Models.SetHidden(children.Where(leaf => !leaf.Equals(keep)), true);
                        var remaining = children.Where(EffectivelyVisible).ToList();
                        if (remaining.Count == 0) continue; // Native instance hiding may also hide keep.
                        var expected = CenterOfLeafUnion(remaining, boxes, scale);
                        if (expected.DistanceTo(originalCenter) <= 0.000001) continue;
                        var actual = session.CenterInMeters(parent);
                        AssertNear(actual.X, expected.X); AssertNear(actual.Y, expected.Y); AssertNear(actual.Z, expected.Z);
                        document.Models.SetHidden(children, true);
                        Assert(!RoutingSession.IsVisibleEndpoint(parent), "Parent whose entire geometry is hidden remained a valid endpoint.");
                        ExpectInvalidWords(() => session.CenterInMeters(parent), "hidden", "visible");
                        verified = true;
                        break;
                    }
                    if (verified || probes > 32) break;
                }
                Assert(verified, "Sample contains no independently hideable parent whose visible geometry centre measurably changes.");
            }
        });
    }

    private static RoutePoint CenterOfLeafUnion(IEnumerable<ModelItem> leaves, Dictionary<ModelItem, double[]> boxes, double metresPerUnit)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        foreach (var leaf in leaves)
        {
            var box = boxes[leaf];
            minX = Math.Min(minX, box[0]); minY = Math.Min(minY, box[1]); minZ = Math.Min(minZ, box[2]);
            maxX = Math.Max(maxX, box[3]); maxY = Math.Max(maxY, box[4]); maxZ = Math.Max(maxZ, box[5]);
        }
        return new RoutePoint((minX + maxX) * 0.5 * metresPerUnit, (minY + maxY) * 0.5 * metresPerUnit, (minZ + maxZ) * 0.5 * metresPerUnit);
    }

    private DuplicateFixture FindIndependentVisibleDuplicate(List<ModelItem> all)
    {
        using (var hidden = new HiddenSnapshot(document, all))
        {
            foreach (var group in all.Where(item => !string.IsNullOrWhiteSpace(item.DisplayName) && RoutingSession.IsVisibleEndpoint(item))
                .GroupBy(item => item.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1)
                .OrderBy(group => group.Count()).Take(100))
            {
                var matches = group.ToList();
                foreach (var keep in matches.Take(8))
                {
                    hidden.Restore();
                    document.Models.SetHidden(matches.Where(item => !item.Equals(keep)), true);
                    if (RoutingSession.IsVisibleEndpoint(keep) && matches.Where(item => !item.Equals(keep)).All(item => !EffectivelyVisible(item)))
                        return new DuplicateFixture { Name = group.Key, Matches = matches, Keep = keep };
                }
            }
        }
        return null;
    }

    private static bool EffectivelyVisible(ModelItem item)
    { return item != null && !item.IsDisposed && item.AncestorsAndSelf.All(ancestor => !ancestor.IsHidden); }

    private void AssertNoVisibleCapturedRoutes(RoutingSession session)
    {
        try
        {
            var captured = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
            Assert(captured.Count == 0 && session.SegmentItems.Count == 0, "Hidden route ancestor left captured geometry available.");
        }
        catch (InvalidOperationException error)
        { Assert(error.Message.IndexOf("visible", StringComparison.OrdinalIgnoreCase) >= 0 || error.Message.IndexOf("hidden", StringComparison.OrdinalIgnoreCase) >= 0, error.Message); }
    }

    private sealed class DuplicateFixture
    { public string Name; public List<ModelItem> Matches; public ModelItem Keep; }

    private sealed class HiddenSnapshot : IDisposable
    {
        private readonly Document document;
        private readonly Dictionary<ModelItem, bool> hidden;
        public HiddenSnapshot(Document document, IEnumerable<ModelItem> items)
        { this.document = document; hidden = items.ToDictionary(item => item, item => item.IsHidden); }
        public void Restore()
        {
            // SetHidden operates on all native instances; snapshot the whole sample
            // so side effects on equivalent instances are restored as well.
            document.Models.SetHidden(hidden.Where(pair => !pair.Value).Select(pair => pair.Key), false);
            document.Models.SetHidden(hidden.Where(pair => pair.Value).Select(pair => pair.Key), true);
            Assert(hidden.All(pair => pair.Key.IsHidden == pair.Value), "Visibility fixture did not restore the original native hidden flags.");
        }
        public void Dispose() { Restore(); }
    }

    private void TestControl()
    {
        Check("dock_metadata_allows_resize_and_sets_useful_initial_size", () =>
        {
            var record = NApp.Plugins.FindPlugin("ClDockPanelUpdate.CONN") as DockPanePluginRecord;
            Assert(record != null && record.IsEnabled, "Dock plugin was not registered/enabled.");
            Assert(!record.FixedSize, "Dock pane still uses the SDK default FixedSize=true.");
            using (var control = new PathFinderControl())
            {
                Assert(control.Width >= 400 && control.Height >= 300, "Initial pane content retained the default 150 by 150 UserControl size.");
                Assert(control.AutoScaleMode != AutoScaleMode.Inherit, "Root DPI/font scaling still depends on an unspecified native parent.");
            }
        });
        Check("host_docking_reset_and_reparent_resize_content_to_fill", () =>
        {
            using (var firstHost = new Panel())
            using (var secondHost = new Panel())
            using (var control = new PathFinderControl())
            {
                firstHost.Size = new System.Drawing.Size(800, 700);
                secondHost.Size = new System.Drawing.Size(1100, 1400);
                // Navisworks resets Dock before assigning the WinForms parent. A
                // standalone DrawToBitmap(size) check did not exercise this step.
                control.Dock = DockStyle.None;
                firstHost.Controls.Add(control);
                firstHost.CreateControl(); control.CreateControl(); firstHost.PerformLayout();
                AssertHostLayout(firstHost, control);
                firstHost.Size = new System.Drawing.Size(1100, 1400);
                firstHost.PerformLayout(); AssertHostLayout(firstHost, control);
                firstHost.Size = new System.Drawing.Size(800, 700);
                firstHost.PerformLayout(); AssertHostLayout(firstHost, control);
                control.Dock = DockStyle.None;
                secondHost.Controls.Add(control);
                secondHost.CreateControl(); secondHost.PerformLayout(); AssertHostLayout(secondHost, control);
                secondHost.Size = new System.Drawing.Size(800, 700);
                secondHost.PerformLayout(); AssertHostLayout(secondHost, control);
                var tabs = ControlsOf(control).OfType<TabControl>().Single();
                tabs.SelectedIndex = 0; control.PerformLayout();
                SavePreview(control, "pathfinder-parented-routes-preview.png");
                tabs.SelectedIndex = 1; control.PerformLayout();
                SavePreview(control, "pathfinder-parented-path-preview.png");
            }
        });
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

    private void TestCableOverlay()
    {
        Check("cable_render_plugin_registered_and_loads", () =>
        {
            var record = NApp.Plugins.FindPlugin("PathFinderRouteOverlay.CONN") as RenderPluginRecord;
            Assert(record != null && record.IsEnabled, "Cable-line render plugin is not registered/enabled.");
            var renderer = record.LoadedPlugin ?? record.LoadPlugin();
            Assert(renderer is RoutePathOverlay, "Cable-line render plugin loaded the wrong type.");
        });
        Check("cable_overlay_converts_metres_to_document_units_and_bounds_all_points", () =>
        {
            var input = new[] { new RoutePoint(-1.5, 2.25, 3.5), new RoutePoint(4.5, -5.25, 6.5), new RoutePoint(7.5, 8.25, -9.5) };
            try
            {
                RoutePathOverlay.Show(document, input);
                AssertOverlayPoints(input);
                AssertNear(UnitConversion.ScaleFactor(Units.Meters, Units.Millimeters), 1000);
                AssertNear(UnitConversion.ScaleFactor(Units.Millimeters, Units.Meters), 0.001);
                var record = (RenderPluginRecord)NApp.Plugins.FindPlugin("PathFinderRouteOverlay.CONN");
                var renderer = (RoutePathOverlay)record.LoadedPlugin;
                using (var box = renderer.MakeRenderBoundingBox(document.ActiveView))
                {
                    Assert(!box.IsEmpty, "Shown cable has an empty rendering bounding box.");
                    foreach (var point in RoutePathOverlay.DisplayedPoints)
                    {
                        Assert(point.X >= box.Min.X && point.X <= box.Max.X && point.Y >= box.Min.Y && point.Y <= box.Max.Y && point.Z >= box.Min.Z && point.Z <= box.Max.Z,
                            "Cable rendering bounds exclude a path point.");
                    }
                }
            }
            finally { RoutePathOverlay.Clear(); }
            Assert(RoutePathOverlay.DisplayedPoints.Count == 0 && !RoutePathOverlay.IsShownFor(document), "Clear retained displayed cable points.");
            var loaded = (RoutePathOverlay)((RenderPluginRecord)NApp.Plugins.FindPlugin("PathFinderRouteOverlay.CONN")).LoadedPlugin;
            using (var box = loaded.MakeRenderBoundingBox(document.ActiveView)) Assert(box.IsEmpty, "Cleared cable retained rendering bounds.");
        });
        Check("scene_plus_overlay_image_contains_visible_yellow_cable_line", () =>
        {
            using (var box = document.GetBoundingBox(false))
            {
                double metres = UnitConversion.ScaleFactor(document.Units, Units.Meters);
                var first = new RoutePoint((box.Min.X + box.Size.X * 0.1) * metres, (box.Min.Y + box.Size.Y * 0.1) * metres, (box.Min.Z + box.Size.Z * 0.1) * metres);
                var middle = new RoutePoint((box.Min.X + box.Size.X * 0.9) * metres, (box.Min.Y + box.Size.Y * 0.1) * metres, (box.Min.Z + box.Size.Z * 0.7) * metres);
                var last = new RoutePoint((box.Min.X + box.Size.X * 0.9) * metres, (box.Min.Y + box.Size.Y * 0.9) * metres, (box.Min.Z + box.Size.Z * 0.9) * metres);
                RoutePathOverlay.Show(document, new[] { first, middle, last });
            }
            try
            {
                using (var scene = document.ActiveView.GenerateImage(ImageGenerationStyle.Scene, 480, 360, true))
                using (var overlay = document.ActiveView.GenerateImage(ImageGenerationStyle.ScenePlusOverlay, 480, 360, true))
                {
                    Assert(scene != null && overlay != null, "Host did not generate scene/overlay images.");
                    scene.Save(Path.Combine(Path.GetDirectoryName(output), "cable-scene.png"), System.Drawing.Imaging.ImageFormat.Png);
                    overlay.Save(Path.Combine(Path.GetDirectoryName(output), "cable-scene-with-overlay.png"), System.Drawing.Imaging.ImageFormat.Png);
                    int sceneYellow = YellowPixels(scene), overlayYellow = YellowPixels(overlay);
                    Assert(overlayYellow > sceneYellow + 3, "Overlay image did not add visible yellow cable pixels: scene=" + sceneYellow + ", overlay=" + overlayYellow);
                }
            }
            finally { RoutePathOverlay.Clear(); }
        });
    }

    private static int YellowPixels(System.Drawing.Bitmap image)
    {
        int total = 0;
        for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
        {
            var color = image.GetPixel(x, y);
            if (color.R > 220 && color.G > 170 && color.B < 80) total++;
        }
        return total;
    }

    private void AssertOverlayPoints(IReadOnlyList<RoutePoint> input)
    {
        Assert(RoutePathOverlay.IsShownFor(document), "Shown cable is not active for this document.");
        var displayed = RoutePathOverlay.DisplayedPoints;
        Assert(displayed.Count == input.Count, "Displayed cable point count differs from calculated path.");
        double scale = UnitConversion.ScaleFactor(Units.Meters, document.Units);
        for (int index = 0; index < input.Count; index++)
        {
            AssertNear(displayed[index].X, input[index].X * scale);
            AssertNear(displayed[index].Y, input[index].Y * scale);
            AssertNear(displayed[index].Z, input[index].Z * scale);
        }
    }

    private static void AssertHostLayout(Panel host, PathFinderControl control)
    {
        control.PerformLayout();
        Assert(control.Dock == DockStyle.Fill, "Attaching the native host left the pane content undocked.");
        Assert(control.Bounds == host.DisplayRectangle, "Pane content does not fill its parent after resize: " + control.Bounds + " vs " + host.DisplayRectangle);
        var tabs = ControlsOf(control).OfType<TabControl>().Single();
        Assert(tabs.Width == control.ClientSize.Width && tabs.Height > control.ClientSize.Height / 2,
            "Tab strip or content retained the tiny default size after parent resize.");
        for (int index = 0; index < tabs.TabPages.Count; index++)
        {
            tabs.SelectedIndex = index; control.PerformLayout(); System.Windows.Forms.Application.DoEvents();
            var page = tabs.SelectedTab;
            var layout = page.Controls.OfType<TableLayoutPanel>().Single();
            Assert(layout.Width >= page.ClientSize.Width - 8, "Selected tab layout did not expand across the native pane.");
            if (index == 0)
            {
                var grid = ControlsOf(page).OfType<DataGridView>().Single();
                Assert(grid.Width >= page.ClientSize.Width - 40 && grid.Height > 60, "Routes table is clipped or collapsed after resize.");
            }
            else
            {
                Assert(Field<TextBox>(control, "from").Width >= 100 && Field<TextBox>(control, "to").Width >= 100,
                    "From or To field collapsed after native pane resize.");
                foreach (var field in new[] { "calculate", "show", "reverse", "restore", "cancel" })
                {
                    var button = Field<Button>(control, field);
                    var bounds = BoundsRelativeTo(button, page);
                    Assert(page.ClientRectangle.Contains(bounds), "Path action is clipped after resize: " + button.Text + " " + bounds);
                }
                Assert(Field<TextBox>(control, "output").Height > 30, "Path output collapsed after native pane resize.");
            }
        }
    }

    private static System.Drawing.Rectangle BoundsRelativeTo(Control child, Control ancestor)
    {
        var location = child.Location;
        var parent = child.Parent;
        while (parent != null && !ReferenceEquals(parent, ancestor))
        { location.Offset(parent.Location); parent = parent.Parent; }
        Assert(parent != null, "Control is not inside the requested tab.");
        return new System.Drawing.Rectangle(location, child.Size);
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
                AssertOverlayPoints(result.PathPoints);
                AssertNear(leaves[0].Geometry.ActiveTransparency, 0); AssertNear(leaves[1].Geometry.ActiveTransparency, 0);
                Invoke(control, "ReversePath");
                AssertOverlayPoints(result.PathPoints.Reverse().ToArray());
                Assert(Field<ModelItem>(control, "resolvedFrom").Equals(leaves[1]) && Field<ModelItem>(control, "resolvedTo").Equals(leaves[0]), "Reverse did not swap resolved objects.");
                AssertColor(leaves[1], 0.10, 0.85, 0.25); AssertColor(leaves[0], 1.00, 0.45, 0.05);
                Invoke(control, "RestoreView"); Assert(!visualization.IsShown, "Restore handler left visualization active.");
                Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Restore handler left the cable overlay active.");
                AssertAppearance(before, true, true);
                Invoke(control, "ShowPath"); Assert(RoutePathOverlay.IsShownFor(document), "Restored result could not be shown again.");
                Field<TextBox>(control, "from").Text = "PF_SMOKE_MISSING_EDIT";
                Assert(Field<RouteResult>(control, "result") == null, "Changing an endpoint kept the old result.");
                Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Endpoint edit retained the previous cable overlay.");
                Assert(!Field<System.Windows.Forms.Timer>(control, "debounce").Enabled, "Paused endpoint edit started an automatic calculation.");
                Assert(Field<Button>(control, "calculate").Enabled, "Pause disabled manual Calculate path after editing.");
            }
            AssertAppearance(before, true, true);
        });
        Check("disposing_control_clears_shown_cable_overlay", () =>
        {
            var before = Snapshot(geometry);
            using (var control = new PathFinderControl())
            {
                document.CurrentSelection.CopyFrom(new[] { leaves[0] }); Wait((Task)Invoke(control, "AssignAsync"));
                document.CurrentSelection.CopyFrom(new[] { leaves[0] }); Invoke(control, "Pick", true);
                document.CurrentSelection.CopyFrom(new[] { leaves[1] }); Invoke(control, "Pick", false);
                Wait((Task)Invoke(control, "CalculateAsync")); Invoke(control, "ShowPath");
                Assert(RoutePathOverlay.IsShownFor(document), "Dispose test did not have a shown cable.");
            }
            Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Disposed control retained the cable overlay.");
            AssertAppearance(before, true, true);
        });
        Check("hiding_endpoint_or_tray_clears_ui_result_and_cable_overlay", () =>
        {
            var all = document.Models.CreateCollectionFromRootItems().DescendantsAndSelf.ToList();
            var before = Snapshot(geometry);
            using (var hidden = new HiddenSnapshot(document, all))
            using (var control = new PathFinderControl())
            {
                document.CurrentSelection.CopyFrom(new[] { leaves[0] }); Wait((Task)Invoke(control, "AssignAsync"));
                document.CurrentSelection.CopyFrom(new[] { leaves[1] }); Invoke(control, "Pick", true);
                document.CurrentSelection.CopyFrom(new[] { leaves[2] }); Invoke(control, "Pick", false);
                Wait((Task)Invoke(control, "CalculateAsync")); Invoke(control, "ShowPath");
                Assert(RoutePathOverlay.IsShownFor(document), "Visibility UI test did not have a shown cable.");
                int assignmentCount = Field<RoutingSession>(control, "session").Assignments.Count;
                document.Models.SetHidden(new[] { leaves[2] }, true); System.Windows.Forms.Application.DoEvents();
                Invoke(control, "ShowPath");
                Assert(Field<RouteResult>(control, "result") == null && !Field<PathVisualization>(control, "visualization").IsShown && RoutePathOverlay.DisplayedPoints.Count == 0,
                    "Hiding the endpoint retained a stale result or visualization.");
                Assert(Field<RoutingSession>(control, "session").Assignments.Count == assignmentCount, "Endpoint visibility change discarded route rules.");
                hidden.Restore(); Wait((Task)Invoke(control, "CalculateAsync")); Invoke(control, "ShowPath");
                Assert(RoutePathOverlay.IsShownFor(document), "Unhiding the endpoint did not permit manual recalculation.");
                document.Models.SetHidden(new[] { leaves[0] }, true); System.Windows.Forms.Application.DoEvents();
                Invoke(control, "ShowPath");
                Assert(Field<RouteResult>(control, "result") == null && !Field<PathVisualization>(control, "visualization").IsShown && RoutePathOverlay.DisplayedPoints.Count == 0,
                    "Hiding a used tray retained a stale result or cable overlay.");
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
    private static void ExpectInvalidWords(Action action, params string[] words)
    {
        try { action(); }
        catch (InvalidOperationException error)
        {
            Assert(words.Any(word => error.Message.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0), "Unreadable visibility error: " + error.Message);
            return;
        }
        throw new Exception("Expected a readable hidden/visible object error.");
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
