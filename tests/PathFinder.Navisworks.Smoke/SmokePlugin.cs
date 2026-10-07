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
            TestPathSelection(leaves);
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
            Check("show_path_opaque_colored_endpoints_and_route_keeps_other_appearance", () =>
            {
                visualization.Show(document, new[] { path }, from, to);
                AssertColor(from, 0.10, 0.85, 0.25); AssertColor(to, 1.00, 0.45, 0.05); AssertColor(path, 0.10, 0.65, 1.00);
                AssertNear(from.Geometry.ActiveTransparency, 0); AssertNear(to.Geometry.ActiveTransparency, 0); AssertNear(path.Geometry.ActiveTransparency, 0);
                AssertAppearance(before.Where(snapshot => !snapshot.Item.Equals(from) && !snapshot.Item.Equals(to)
                    && !snapshot.Item.Equals(path)).ToList(), true, true);
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
            Check("unrelated_temporary_appearance_edits_survive_restore_and_dispose", () =>
            {
                var saved = Snapshot(new[] { background })[0];
                try
                {
                    visualization.Show(document, new[] { path }, from, to);
                    using (var color = new Color(0.12, 0.23, 0.34)) document.Models.OverrideTemporaryColor(new[] { background }, color);
                    document.Models.OverrideTemporaryTransparency(new[] { background }, 0.58);
                    visualization.Restore();
                    AssertColor(background, 0.12, 0.23, 0.34); AssertNear(background.Geometry.ActiveTransparency, 0.58);
                    using (var disposable = new PathVisualization())
                    {
                        disposable.Show(document, new[] { path }, from, to);
                        AssertColor(background, 0.12, 0.23, 0.34); AssertNear(background.Geometry.ActiveTransparency, 0.58);
                        document.Models.OverrideTemporaryTransparency(new[] { background }, 0.67);
                    }
                    AssertColor(background, 0.12, 0.23, 0.34); AssertNear(background.Geometry.ActiveTransparency, 0.67);
                }
                finally
                {
                    visualization.Restore();
                    document.Models.ResetTemporaryMaterials(new[] { background });
                    if (!saved.ActiveColor.SequenceEqual(saved.PermanentColor))
                        using (var color = new Color(saved.ActiveColor[0], saved.ActiveColor[1], saved.ActiveColor[2]))
                            document.Models.OverrideTemporaryColor(new[] { background }, color);
                    if (saved.ActiveTransparency != saved.PermanentTransparency)
                        document.Models.OverrideTemporaryTransparency(new[] { background }, saved.ActiveTransparency);
                }
            });
        }
    }

    private void TestPathSelection(List<ModelItem> leaves)
    {
        var from = leaves[0]; var to = leaves[1]; var first = leaves[2]; var second = leaves[3]; var previous = leaves[4];
        Check("show_path_selects_exact_distinct_route_leaves_without_extra_endpoints", () =>
        {
            document.CurrentSelection.CopyFrom(new[] { previous });
            var before = Snapshot(geometry);
            using (var visualization = new PathVisualization())
            {
                visualization.Show(document, new[] { first, second, first, null }, from, to);
                AssertSelection(first, second);
                AssertColor(from, 0.10, 0.85, 0.25); AssertColor(to, 1.00, 0.45, 0.05);
                AssertNear(first.Geometry.ActiveTransparency, 0); AssertNear(second.Geometry.ActiveTransparency, 0);
                using (var image = document.ActiveView.GenerateImage(ImageGenerationStyle.ScenePlusOverlay, 800, 600, true))
                {
                    Assert(image != null, "Selected path scene did not render.");
                    image.Save(Path.Combine(Path.GetDirectoryName(output), "path-selected-scene.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
                document.CurrentSelection.CopyFrom(new ModelItem[0]);
                using (var image = document.ActiveView.GenerateImage(ImageGenerationStyle.ScenePlusOverlay, 800, 600, true))
                {
                    Assert(image != null, "Unselected comparison scene did not render.");
                    image.Save(Path.Combine(Path.GetDirectoryName(output), "path-unselected-scene.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
                document.CurrentSelection.CopyFrom(new[] { second, first });
                visualization.Restore();
                AssertSelection(previous);
            }
            AssertAppearance(before, true, true);
        });
        Check("repeated_show_and_reverse_keep_original_selection_and_restore_by_identity_set", () =>
        {
            var parent = previous.Parent ?? previous;
            document.CurrentSelection.CopyFrom(new[] { parent });
            using (var visualization = new PathVisualization())
            {
                visualization.Show(document, new[] { first, second }, from, to);
                visualization.Show(document, new[] { second, first }, to, from);
                AssertSelection(first, second);
                visualization.Show(document, new[] { first }, from, to);
                AssertSelection(first);
                visualization.Restore(); AssertSelection(parent);
                visualization.Restore(); AssertSelection(parent);
            }
        });
        Check("restore_and_dispose_preserve_later_manual_selection_or_clear", () =>
        {
            document.CurrentSelection.CopyFrom(new[] { previous });
            using (var visualization = new PathVisualization())
            {
                visualization.Show(document, new[] { first }, from, to);
                document.CurrentSelection.CopyFrom(new[] { to, previous });
                visualization.Restore(); AssertSelection(to, previous);
                visualization.Show(document, new[] { second }, from, to);
                document.CurrentSelection.CopyFrom(new ModelItem[0]);
                visualization.Restore(); AssertSelection();
                visualization.Show(document, new[] { first }, from, to);
                document.CurrentSelection.CopyFrom(new[] { from });
            }
            AssertSelection(from);
        });
        Check("dispose_restores_original_empty_selection_and_materials", () =>
        {
            document.CurrentSelection.CopyFrom(new ModelItem[0]);
            var before = Snapshot(geometry);
            using (var visualization = new PathVisualization())
            {
                visualization.Show(document, new[] { first }, from, to);
                AssertSelection(first);
            }
            AssertSelection(); AssertAppearance(before, true, true);
        });
    }

    private void AssertSelection(params ModelItem[] expected)
    {
        Assert(new HashSet<ModelItem>(expected).SetEquals(document.CurrentSelection.SelectedItems),
            "Native Selection Tree selection differs from expected model items.");
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
            Check("hierarchy_route_labels_flow_through_native_capture_with_selected_root_bounds", () =>
            {
                Assert(RouteNameResolver.Resolve(new[] { "Tube /B1", "BRANCH /5LD04/B1", "/5LD04", "/ROUTE_ZONE" }) == "/5LD04",
                    "Digit-leading logical route failed in the installed Framework assembly.");
                var segments = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                foreach (var segment in segments)
                {
                    var item = session.SegmentItems[segment.Id];
                    var names = new List<string>();
                    foreach (var ancestor in item.AncestorsAndSelf)
                    {
                        names.Add(ancestor.DisplayName);
                        if (ancestor.Equals(nested)) break;
                    }
                    Assert(segment.RouteCode == RouteNameResolver.Resolve(names), "Native capture did not use the bounded logical hierarchy.");
                }
                document.CurrentSelection.CopyFrom(new[] { leaf });
                using (var bounded = new RoutingSession())
                {
                    Wait(bounded.AssignSelectionAsync(CableCategory.LV, CancellationToken.None));
                    var single = Wait(bounded.CaptureSegmentsAsync(CancellationToken.None));
                    Assert(single.Count == 1 && single[0].RouteCode == RouteNameResolver.Resolve(new[] { leaf.DisplayName }),
                        "A selected leaf used names above its assigned root.");
                }
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
            Check("bottom_face_base_point_converts_document_units_to_meters", () =>
            {
                var endpoint = leaves.FirstOrDefault(item => VisibleHeightInMeters(item) > 0.000001);
                Assert(endpoint != null, "Sample has no visible endpoint with measurable height.");
                double scale = UnitConversion.ScaleFactor(document.Units, Units.Meters);
                using (var box = endpoint.BoundingBox(true))
                using (var minimum = box.Min)
                using (var maximum = box.Max)
                {
                    var metres = session.BasePointInMeters(endpoint);
                    AssertNear(metres.X, (minimum.X + maximum.X) * 0.5 * scale);
                    AssertNear(metres.Y, (minimum.Y + maximum.Y) * 0.5 * scale);
                    AssertNear(metres.Z, minimum.Z * scale);
                    Assert(Math.Abs(metres.Z - (minimum.Z + maximum.Z) * 0.5 * scale) > 0.0000005,
                        "Endpoint base point was indistinguishable from its full-height centre.");
                }
                AssertNear(UnitConversion.ScaleFactor(Units.Millimeters, Units.Meters), 0.001);
                AssertNear(UnitConversion.ScaleFactor(Units.Feet, Units.Meters), 0.3048);
            });
            Check("item_transform_fingerprint_detects_stale_captured_path", () =>
            {
                var fingerprint = typeof(RoutingSession).GetMethod("AreCapturedSegmentsCurrent");
                Assert(fingerprint != null, "Captured geometry fingerprint method is missing.");
                Assert((bool)fingerprint.Invoke(session, null), "Fresh captures unexpectedly stale.");
                using (var before = leaf.BoundingBox())
                {
                    try
                    {
                        using (var translation = Transform3D.CreateTranslation(new Vector3D(0.125, 0, 0)))
                            document.Models.OverridePermanentTransform(new[] { leaf }, translation, true);
                        Assert(!(bool)fingerprint.Invoke(session, null), "Changed item bounding box was accepted as current.");
                    }
                    finally { document.Models.ResetPermanentTransform(new[] { leaf }); }
                    using (var reset = leaf.BoundingBox()) AssertBounds(reset, before);
                }
                // Native Reset can numerically rebase the active matrix even when
                // the physical bounds are restored. Capture the restored geometry.
                Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert((bool)fingerprint.Invoke(session, null), "A fresh capture after resetting the item was unexpectedly stale.");
            });
            Check("half_turn_with_unchanged_world_box_invalidates_capture", () =>
            {
                Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(session.AreCapturedSegmentsCurrent(), "The half-turn fixture was stale before applying its rotation.");
                using (var before = leaf.BoundingBox())
                {
                    var centre = before.Center;
                    try
                    {
                        using (var linear = new Matrix3(-1, 0, 0, 0, -1, 0, 0, 0, 1))
                        using (var translation = new Vector3D(2 * centre.X, 2 * centre.Y, 0))
                        using (var transform = new Transform3D(linear, translation))
                            document.Models.OverridePermanentTransform(new[] { leaf }, transform, false);
                        using (var after = leaf.BoundingBox()) AssertBounds(after, before);
                        Assert(!session.AreCapturedSegmentsCurrent(), "A changed transform with unchanged world bounds retained its old ports.");
                    }
                    finally { document.Models.ResetPermanentTransform(new[] { leaf }); }
                    using (var reset = leaf.BoundingBox()) AssertBounds(reset, before);
                }
                Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(session.AreCapturedSegmentsCurrent(), "A fresh capture after resetting the half-turn was unexpectedly stale.");
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
            Check("native_name_search_caches_only_requested_names_and_reuses_case_variants", () =>
            {
                var cache = Field<Dictionary<string, List<ModelItem>>>(session, "nameIndex");
                Assert(cache.Count == 2 && cache.ContainsKey(duplicate.Key), "Name search unexpectedly indexed the full model.");
                var matches = cache[duplicate.Key];
                ExpectInvalid(() => Wait(session.ResolveAsync("  " + duplicate.Key.ToUpperInvariant() + "  ", null, CancellationToken.None)), "More than one visible object");
                Assert(cache.Count == 2 && ReferenceEquals(matches, cache[duplicate.Key]), "Case/trim variants did not reuse the existing native search result.");
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
        Check("warm_geometry_fit_reuses_unchanged_inputs_and_rebuilds_for_live_category_and_visibility", () =>
        {
            using (var hidden = new HiddenSnapshot(document, all))
            using (var session = new RoutingSession())
            {
                var selected = leaves.Where(EffectivelyVisible).Take(3).ToArray();
                Assert(selected.Length == 3, "Sample has fewer than three visible route leaves.");
                document.CurrentSelection.CopyFrom(selected);
                Wait(session.AssignSelectionAsync(CableCategory.LV, CancellationToken.None));
                var first = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                int firstBuilds = session.GeometryBuildCount;
                Assert(first.Count == 3 && firstBuilds == 1, "Initial detached geometry was not built once.");
                var warm = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(session.GeometryBuildCount == firstBuilds, "Unchanged warm capture refitted the same geometry.");
                Assert(first.Zip(warm, (a, b) => a.Id == b.Id && a.AllowedCategories == b.AllowedCategories
                    && a.Points.SequenceEqual(b.Points) && a.ConnectionsAtEndsOnly == b.ConnectionsAtEndsOnly).All(value => value),
                    "Warm capture changed detached geometry or categories.");
                Assert(session.AreCapturedSegmentsCurrent(), "Warm capture discarded the live geometry fingerprints.");

                int revision = session.Revision;
                session.Assignments[0].Categories = CableCategory.Control;
                var recategorized = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(session.Revision == revision, "Direct category fixture unexpectedly called NotifyChanged.");
                Assert(session.GeometryBuildCount == firstBuilds + 1, "A changed live category reused an obsolete fit.");
                Assert(recategorized.Single(segment => session.SegmentItems[segment.Id].Equals(selected[0])).AllowedCategories == CableCategory.Control,
                    "Recapture did not apply the changed live category.");
                Assert(recategorized.Where(segment => !session.SegmentItems[segment.Id].Equals(selected[0]))
                    .All(segment => segment.AllowedCategories == CableCategory.LV), "Category change affected other route assignments.");
                Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(session.GeometryBuildCount == firstBuilds + 1, "Recategorized geometry was refitted on its unchanged warm capture.");

                document.Models.SetHidden(new[] { selected[0] }, true);
                System.Windows.Forms.Application.DoEvents();
                Assert(!session.AreCapturedSegmentsCurrent(), "Hiding a fitted leaf preserved a stale current snapshot.");
                var visible = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(visible.Count == 2 && session.GeometryBuildCount == firstBuilds + 2, "Hide did not rebuild the visible detached network.");
                document.Models.SetHidden(new[] { selected[0] }, false);
                System.Windows.Forms.Application.DoEvents();
                var restored = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(restored.Count == 3 && session.GeometryBuildCount == firstBuilds + 3, "Unhide did not rebuild the restored network.");
                Assert(session.AreCapturedSegmentsCurrent(), "Restored network has stale fingerprints.");
                Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                Assert(session.GeometryBuildCount == firstBuilds + 3, "Restored unchanged network refitted during warm capture.");
            }
        });
        Check("visible_parent_base_point_excludes_hidden_lower_geometry", () =>
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
                    var originalBase = BasePointOfLeafUnion(children, boxes, scale);
                    var candidates = children.OrderByDescending(leaf => boxes[leaf][2]).Take(8);
                    foreach (var keep in candidates)
                    {
                        if (boxes[keep][2] * scale <= originalBase.Z + 0.000001) continue;
                        if (++probes > 32) break;
                        hidden.Restore();
                        document.Models.SetHidden(children.Where(leaf => !leaf.Equals(keep)), true);
                        var remaining = children.Where(EffectivelyVisible).ToList();
                        if (remaining.Count == 0) continue; // Native instance hiding may also hide keep.
                        var expected = BasePointOfLeafUnion(remaining, boxes, scale);
                        if (expected.Z <= originalBase.Z + 0.000001) continue;
                        Assert(children.Any(leaf => !EffectivelyVisible(leaf) && boxes[leaf][2] * scale < expected.Z - 0.000001),
                            "Visible-parent fixture did not hide geometry below the remaining bottom face.");
                        var actual = session.BasePointInMeters(parent);
                        AssertNear(actual.X, expected.X); AssertNear(actual.Y, expected.Y); AssertNear(actual.Z, expected.Z);
                        document.Models.SetHidden(children, true);
                        Assert(!RoutingSession.IsVisibleEndpoint(parent), "Parent whose entire geometry is hidden remained a valid endpoint.");
                        ExpectInvalidWords(() => session.BasePointInMeters(parent), "hidden", "visible");
                        verified = true;
                        break;
                    }
                    if (verified || probes > 32) break;
                }
                Assert(verified, "Sample contains no independently hideable parent whose visible bottom face rises when lower geometry is hidden.");
            }
        });
    }

    private static RoutePoint BasePointOfLeafUnion(IEnumerable<ModelItem> leaves, Dictionary<ModelItem, double[]> boxes, double metresPerUnit)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var leaf in leaves)
        {
            var box = boxes[leaf];
            minX = Math.Min(minX, box[0]); minY = Math.Min(minY, box[1]); minZ = Math.Min(minZ, box[2]);
            maxX = Math.Max(maxX, box[3]); maxY = Math.Max(maxY, box[4]);
        }
        return new RoutePoint((minX + maxX) * 0.5 * metresPerUnit, (minY + maxY) * 0.5 * metresPerUnit, minZ * metresPerUnit);
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
                firstHost.Size = new System.Drawing.Size(550, 650);
                firstHost.PerformLayout();
                SavePreview(control, "pathfinder-narrow-path-preview.png");
                AssertHostLayout(firstHost, control);
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
        Check("two_tabs_use_permanent_manual_mode_without_pause_checkbox_or_timer", () =>
        {
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700);
                control.CreateControl();
                control.PerformLayout();
                var descendants = ControlsOf(control).ToList();
                var tabs = descendants.OfType<TabControl>().Single();
                Assert(tabs.TabPages.Count == 2 && tabs.TabPages[0].Text == "Routes" && tabs.TabPages[1].Text == "Path", "Expected Routes and Path tabs.");
                AssertManualMode(control);
                Assert(descendants.OfType<Button>().Single(item => item.Text == "Calculate path").Enabled, "Manual Calculate path disabled.");
                Assert((string)descendants.OfType<ComboBox>().Single().SelectedItem == "LV", "Cable type default is not LV.");
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
        Check("design_allowance_fields_defaults_precision_and_busy_state", () =>
        {
            using (var control = new PathFinderControl())
            {
                var spare = Field<NumericUpDown>(control, "connectionSpare");
                var secondary = Field<NumericUpDown>(control, "secondaryLength");
                var percent = Field<NumericUpDown>(control, "lengthAllowance");
                Assert(spare.Value == 6m && secondary.Value == 0m && percent.Value == 0m, "Design allowance defaults differ.");
                var fields = new[] { spare, secondary, percent };
                foreach (var field in fields)
                {
                    Assert(field.Minimum == 0m && field.DecimalPlaces == 2, "Design allowance accepts negative or hidden-precision values.");
                    field.Text = 1.235m.ToString(System.Globalization.CultureInfo.CurrentCulture);
                    Assert(field.Value == 1.24m, "Typed design allowance did not round to its visible value.");
                }
                try
                {
                    Invoke(control, "StartOperation", "Testing design field state.");
                    Assert(fields.All(field => !field.Enabled), "Design allowances remain editable during capture.");
                }
                finally { Invoke(control, "FinishOperation"); }
                Assert(fields.All(field => field.Enabled), "Design allowances did not re-enable after capture.");
                AssertManualMode(control);
            }
        });
        Check("secondary_distance_field_has_default_range_and_busy_state", () =>
        {
            using (var control = new PathFinderControl())
            {
                var threshold = Field<NumericUpDown>(control, "secondaryDistance");
                Assert(threshold.Value == 2m, "SECONDARY distance does not default to two metres.");
                Assert(threshold.Minimum == 0m && threshold.Maximum == 1000m && threshold.DecimalPlaces == 2,
                    "SECONDARY distance range or precision differs from the supported UI contract.");
                Assert(threshold.Increment == 0.05m && threshold.Enabled, "SECONDARY distance step or initial enabled state differs.");
                threshold.Text = 1.234m.ToString(System.Globalization.CultureInfo.CurrentCulture);
                Assert(threshold.Value == 1.23m, "Typed three-decimal threshold retained precision hidden by its display.");
                threshold.Text = 1.235m.ToString(System.Globalization.CultureInfo.CurrentCulture);
                Assert(threshold.Value == 1.24m, "Typed threshold did not round its midpoint away from zero.");
                threshold.Value = 2m;
                try
                {
                    Invoke(control, "StartOperation", "Testing field state during capture.");
                    Assert(Field<bool>(control, "busy") && !threshold.Enabled, "SECONDARY distance can change while an operation is busy.");
                }
                finally { Invoke(control, "FinishOperation"); }
                Assert(!Field<bool>(control, "busy") && threshold.Enabled, "SECONDARY distance was not restored after finishing the operation.");
            }
        });
        Check("vertical_equipment_approach_defaults_on_and_is_locked_while_busy", () =>
        {
            Assert(!new RoutingOptions().PreferVerticalApproach, "The library default changed the existing shortest-3D attachment contract.");
            using (var control = new PathFinderControl())
            {
                var approach = Field<CheckBox>(control, "verticalApproach");
                Assert(approach.Checked && approach.Enabled && !approach.ThreeState,
                    "The UI approach option is not an enabled two-state checkbox checked by default.");
                approach.Checked = false;
                Assert(!approach.Checked, "Selecting shortest-3D approach did not retain the option.");
                AssertManualMode(control);
                try
                {
                    Invoke(control, "StartOperation", "Testing approach state during capture.");
                    Assert(Field<bool>(control, "busy") && !approach.Enabled, "Approach preference can change while an operation is busy.");
                }
                finally { Invoke(control, "FinishOperation"); }
                Assert(approach.Enabled && !approach.Checked, "Finishing an operation lost the selected approach preference.");
            }
        });
        Check("connection_gap_metrics_render_and_reverse_preserve_totals", () =>
        {
            var calculated = new RouteCalculator().Calculate(new[]
            {
                new TraySegment("gap-ui-first", "/B001", CableCategory.LV,
                    new[] { new RoutePoint(0, 0, 0), new RoutePoint(1, 0, 0) }),
                new TraySegment("gap-ui-last", "/B002", CableCategory.LV,
                    new[] { new RoutePoint(1.15, 0, 0), new RoutePoint(2.15, 0, 0) })
            }, new RoutePoint(0, 0, 0), new RoutePoint(2.15, 0, 0), CableCategory.LV,
                new RoutingOptions { ConnectionToleranceMeters = 0.25 });
            Assert(calculated.Success && calculated.ConnectionGapCount == 1, "Positive-gap UI fixture did not contain exactly one connection gap.");
            AssertNear(calculated.ConnectionGapLengthMeters, 0.15);
            using (var control = new PathFinderControl())
            {
                // Use a known detached result to exercise the native WinForms
                // output without depending on accidental sample-model gaps.
                typeof(PathFinderControl).GetField("result", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(control, calculated);
                Invoke(control, "RenderResult");
                AssertGapMetricsOutput(control, calculated);
                Invoke(control, "ReversePath");
                var reversed = Field<RouteResult>(control, "result");
                Assert(reversed.ConnectionGapCount == calculated.ConnectionGapCount
                    && reversed.ConnectionGapLengthMeters == calculated.ConnectionGapLengthMeters, "Reverse changed connection-gap totals.");
                AssertNear(reversed.LengthMeters, calculated.LengthMeters);
                AssertGapMetricsOutput(control, reversed);
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

    private static void AssertManualMode(PathFinderControl control)
    {
        Assert(!ControlsOf(control).OfType<CheckBox>().Any(item => item.Text == "Pause automatic calculation"),
            "The removed Pause checkbox remains in the panel.");
        Assert(!typeof(PathFinderControl).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(System.Windows.Forms.Timer)),
            "The permanent manual mode still owns an automatic calculation timer.");
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
            page.AutoScrollPosition = System.Drawing.Point.Empty;
            var layout = page.Controls.OfType<TableLayoutPanel>().Single();
            int availableWidth = page.ClientSize.Width - (page.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0);
            Assert(layout.Width >= availableWidth - 8, "Selected tab layout did not expand across the native pane.");
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
                    page.ScrollControlIntoView(button);
                    System.Windows.Forms.Application.DoEvents();
                    var bounds = BoundsRelativeTo(button, page);
                    Assert(page.ClientRectangle.Contains(bounds), "Path action is clipped after resize: " + button.Text + " " + bounds);
                }
                foreach (var option in new[] { Field<NumericUpDown>(control, "gap"), Field<NumericUpDown>(control, "secondaryDistance"),
                    Field<NumericUpDown>(control, "connectionSpare"), Field<NumericUpDown>(control, "secondaryLength"),
                    Field<NumericUpDown>(control, "lengthAllowance") })
                {
                    page.ScrollControlIntoView(option.Parent);
                    System.Windows.Forms.Application.DoEvents();
                    Assert(page.ClientRectangle.Contains(BoundsRelativeTo(option, page)), "Path numeric option is clipped after resize.");
                    var label = option.Parent.Controls.OfType<Label>().Single();
                    Assert(page.ClientRectangle.Contains(BoundsRelativeTo(label, page)), "Path numeric option label is clipped after resize.");
                    Assert(BoundsRelativeTo(label.Parent, page).Contains(BoundsRelativeTo(option, page)), "Numeric option escaped its label group.");
                }
                page.ScrollControlIntoView(Field<CheckBox>(control, "verticalApproach"));
                System.Windows.Forms.Application.DoEvents();
                Assert(page.ClientRectangle.Contains(BoundsRelativeTo(Field<CheckBox>(control, "verticalApproach"), page)),
                    "The equipment-approach checkbox is clipped after native pane resize.");
                var pathOutput = Field<TextBox>(control, "output");
                Assert(pathOutput.Height > 30, "Path output collapsed after native pane resize: output=" + pathOutput.Size
                    + ", control=" + control.ClientSize + ", page=" + page.ClientSize + ", layout=" + layout.Size
                    + ", row heights=" + string.Join(",", layout.GetRowHeights()) + ".");
                page.ScrollControlIntoView(pathOutput);
                System.Windows.Forms.Application.DoEvents();
                Assert(page.ClientRectangle.IntersectsWith(BoundsRelativeTo(pathOutput, page)),
                    "The result remains inaccessible after scrolling the narrow pane.");
                page.AutoScrollPosition = System.Drawing.Point.Empty;
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
        Check("vertical_approach_toggle_passes_both_modes_to_manual_calculation_and_reverse_renderer", () =>
        {
            var before = Snapshot(geometry);
            var modifiedItems = new List<ModelItem>();
            var originalBounds = new List<BoundingBox3D>();
            try
            {
                using (var control = new PathFinderControl())
                {
                    var session = Field<RoutingSession>(control, "session");
                    // Discover two real native leaves whose detached geometry is a
                    // nondegenerate line. Translate/rotate only this disposable
                    // smoke model so the two attachment modes must choose different
                    // contacts; unchanged sample geometry cannot prove option wiring.
                    document.CurrentSelection.CopyFrom(leaves.Where(RoutingSession.IsVisibleEndpoint).Take(32));
                    Wait((Task)Invoke(control, "AssignAsync"));
                    var initial = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                    var pair = initial.Where(segment => segment.Points.Count == 2 && segment.Points[0].DistanceTo(segment.Points[1]) > 0.02
                        && !session.SegmentItems[segment.Id].AncestorsAndSelf.Any(item => Regex.IsMatch(item.DisplayName ?? "", @"\b(?:BEND|ELBOW)\b", RegexOptions.IgnoreCase)))
                        .Take(2).ToArray();
                    Assert(pair.Length == 2, "Smoke sample needs two real nondegenerate line leaves for the approach fixture.");
                    var nearTray = session.SegmentItems[pair[0].Id]; var verticalTray = session.SegmentItems[pair[1].Id];
                    var endpoints = leaves.Where(item => !item.Equals(nearTray) && !item.Equals(verticalTray) && RoutingSession.IsVisibleEndpoint(item)).Take(2).ToArray();
                    Assert(endpoints.Length == 2, "Smoke sample needs two separate endpoint leaves for the approach fixture.");
                    modifiedItems.AddRange(new[] { nearTray, verticalTray, endpoints[0], endpoints[1] });
                    foreach (var item in modifiedItems) originalBounds.Add(item.BoundingBox());
                    session.Assignments.Clear(); session.NotifyChanged();
                    document.CurrentSelection.CopyFrom(new[] { nearTray, verticalTray }); Wait((Task)Invoke(control, "AssignAsync"));
                    var selected = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                    foreach (var segment in selected) RotateSmokeLineToXAxis(session.SegmentItems[segment.Id], segment);
                    selected = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                    foreach (var segment in selected)
                    {
                        var centre = Midpoint(segment.Points.First(), segment.Points.Last());
                        var target = session.SegmentItems[segment.Id].Equals(nearTray) ? new RoutePoint(0, 0.10, 0) : new RoutePoint(0, 0, 0.20);
                        TranslateSmokeItemInMeters(session.SegmentItems[segment.Id], centre, target);
                    }
                    selected = Wait(session.CaptureSegmentsAsync(CancellationToken.None));
                    double commonHalfLength = selected.Min(segment => segment.Points.First().DistanceTo(segment.Points.Last())) * 0.5;
                    var expectedFrom = new RoutePoint(0, 0, 0); var expectedTo = new RoutePoint(commonHalfLength * 0.25, 0, 0.20);
                    TranslateSmokeItemInMeters(endpoints[0], NativeBasePointInMeters(endpoints[0]), expectedFrom);
                    TranslateSmokeItemInMeters(endpoints[1], NativeBasePointInMeters(endpoints[1]), expectedTo);
                    document.CurrentSelection.CopyFrom(new[] { endpoints[0] }); Invoke(control, "Pick", true);
                    document.CurrentSelection.CopyFrom(new[] { endpoints[1] }); Invoke(control, "Pick", false);
                    var approach = Field<CheckBox>(control, "verticalApproach");
                    Field<NumericUpDown>(control, "gap").Value = 0.25m;
                    var nearest = new RouteCalculator().Calculate(selected, expectedFrom, expectedTo, CableCategory.LV,
                        new RoutingOptions { ConnectionToleranceMeters = 0.25, PreferVerticalApproach = false });
                    var vertical = new RouteCalculator().Calculate(selected, expectedFrom, expectedTo, CableCategory.LV,
                        new RoutingOptions { ConnectionToleranceMeters = 0.25, PreferVerticalApproach = true });
                    Assert(nearest.Success && vertical.Success, "The two native fixture trays are not connected at the selected gap.");
                    Assert(Math.Abs(nearest.FromDistanceMeters - vertical.FromDistanceMeters) > 0.05
                        && nearest.PathPoints[1].DistanceTo(vertical.PathPoints[1]) > 0.05,
                        "The native fixture does not distinguish nearest-3D and vertical attachment modes.");
                    AssertNear(nearest.FromDistanceMeters, 0.10); AssertNear(vertical.FromDistanceMeters, 0.20);
                    AssertPoint(vertical.PathPoints[1], new RoutePoint(0, 0, 0.20));
                    foreach (bool preferVertical in new[] { true, false })
                    {
                        approach.Checked = preferVertical;
                        AssertManualMode(control);
                        Wait((Task)Invoke(control, "CalculateAsync"));
                        var actual = Field<RouteResult>(control, "result"); var expected = preferVertical ? vertical : nearest;
                        AssertUiBasePoints(control, actual, expectedFrom, expectedTo);
                        AssertNear(actual.FromDistanceMeters, expected.FromDistanceMeters); AssertNear(actual.ToDistanceMeters, expected.ToDistanceMeters);
                        AssertNear(actual.LengthMeters, expected.LengthMeters); AssertPoint(actual.PathPoints[1], expected.PathPoints[1]);
                        Assert(actual.ConnectionGapCount == expected.ConnectionGapCount, "Manual calculation ignored the selected approach mode's graph contacts.");
                        Invoke(control, "ShowPath"); AssertOverlayPoints(actual.PathPoints);
                        Invoke(control, "ReversePath");
                        var reversed = Field<RouteResult>(control, "result");
                        AssertUiBasePoints(control, reversed, expectedTo, expectedFrom);
                        AssertOverlayPoints(actual.PathPoints.Reverse().ToArray());
                        AssertPoint(reversed.PathPoints[reversed.PathPoints.Count - 2], expected.PathPoints[1]);
                        AssertNear(reversed.ToDistanceMeters, actual.FromDistanceMeters);
                        Assert(approach.Checked == preferVertical, "Reverse changed the selected approach mode.");
                        Invoke(control, "ReversePath");
                        AssertUiBasePoints(control, Field<RouteResult>(control, "result"), expectedFrom, expectedTo);
                        // Flip the selected option while a result and overlay exist.
                        approach.Checked = !preferVertical;
                        Assert(Field<RouteResult>(control, "result") == null && !Field<Button>(control, "show").Enabled,
                            "Changing approach mode retained a stale calculation.");
                        Assert(!Field<PathVisualization>(control, "visualization").IsShown && RoutePathOverlay.DisplayedPoints.Count == 0,
                            "Changing approach mode retained the previous rendered attachments.");
                        AssertManualMode(control);
                        Assert(Field<Button>(control, "calculate").Enabled, "Approach edit disabled manual Calculate.");
                    }
                    approach.Checked = !approach.Checked;
                    // Valid routes and endpoints remain assigned. Pump the host
                    // beyond the former debounce delay; edits must stay manual.
                    Field<TextBox>(control, "from").Text += " ";
                    Field<ComboBox>(control, "cableType").SelectedIndex = 2;
                    Field<NumericUpDown>(control, "gap").Value = 0.30m;
                    Field<NumericUpDown>(control, "secondaryDistance").Value = 3m;
                    Invoke(control, "ReversePath");
                    Wait(Task.Delay(1000));
                    AssertManualMode(control);
                    Assert(!Field<bool>(control, "busy") && Field<RouteResult>(control, "result") == null,
                        "Editing valid inputs or reversing an empty result started automatic calculation.");
                    Assert(Field<Button>(control, "calculate").Enabled, "Manual Calculate became unavailable after edits.");
                }
            }
            finally
            {
                RoutePathOverlay.Clear();
                for (int index = 0; index < modifiedItems.Count; index++)
                {
                    document.Models.ResetPermanentTransform(new[] { modifiedItems[index] });
                    using (var restored = modifiedItems[index].BoundingBox()) AssertBounds(restored, originalBounds[index]);
                    originalBounds[index].Dispose();
                }
            }
            AssertAppearance(before, true, true);
        });
        Check("manual_ui_handlers_assign_pick_calculate_show_reverse_restore", () =>
        {
            var before = Snapshot(geometry);
            var endpoints = leaves.Where(item => VisibleHeightInMeters(item) > 0.000001).Take(2).ToList();
            Assert(endpoints.Count == 2, "Sample has fewer than two visible endpoints with measurable height.");
            var fromLeaf = endpoints[0]; var toLeaf = endpoints[1];
            var expectedFrom = NativeBasePointInMeters(fromLeaf); var expectedTo = NativeBasePointInMeters(toLeaf);
            using (var control = new PathFinderControl())
            {
                control.Size = new System.Drawing.Size(800, 700);
                control.CreateControl();
                document.CurrentSelection.CopyFrom(new[] { fromLeaf });
                Wait((Task)Invoke(control, "AssignAsync"));
                var session = Field<RoutingSession>(control, "session");
                Assert(session.Assignments.Count == 1 && session.Assignments[0].LeafCount == 1, "UI did not capture selected route leaf.");
                document.CurrentSelection.CopyFrom(new[] { fromLeaf }); Invoke(control, "Pick", true);
                document.CurrentSelection.CopyFrom(new[] { toLeaf }); Invoke(control, "Pick", false);
                AssertManualMode(control);
                Wait((Task)Invoke(control, "CalculateAsync"));
                var result = Field<RouteResult>(control, "result");
                Assert(result != null && result.Success, "Manual calculation failed: " + Field<TextBox>(control, "output").Text);
                AssertUiBasePoints(control, result, expectedFrom, expectedTo);
                Assert(result.ConnectionGapCount == 0 && result.ConnectionGapLengthMeters == 0,
                    "A single native tray counted equipment attachment legs as connection gaps.");
                AssertGapMetricsOutput(control, result);
                Assert(Field<Button>(control, "show").Enabled, "Show path was not enabled after successful calculation.");
                var selectionBeforeShow = document.CurrentSelection.SelectedItems.ToArray();
                int revisionBeforeShow = session.Revision; int buildsBeforeShow = session.GeometryBuildCount;
                Invoke(control, "ShowPath");
                var visualization = Field<PathVisualization>(control, "visualization");
                Assert(visualization.IsShown, "Show path handler did not apply visualization.");
                AssertSelection(result.SegmentIds.Select(id => session.SegmentItems[id]).ToArray());
                Assert(session.Revision == revisionBeforeShow && session.GeometryBuildCount == buildsBeforeShow
                    && ReferenceEquals(result, Field<RouteResult>(control, "result")) && !Field<bool>(control, "busy"),
                    "Native path selection invalidated the result or started a calculation.");
                AssertOverlayPoints(result.PathPoints);
                AssertNear(fromLeaf.Geometry.ActiveTransparency, 0); AssertNear(toLeaf.Geometry.ActiveTransparency, 0);
                AssertAppearance(before.Where(snapshot => !snapshot.Item.Equals(fromLeaf) && !snapshot.Item.Equals(toLeaf)).ToList(), true, true);
                int designBuildCount = session.GeometryBuildCount;
                Field<NumericUpDown>(control, "secondaryLength").Value = 5m;
                Field<NumericUpDown>(control, "lengthAllowance").Value = 10m;
                decimal expectedDesignBase = (decimal)result.LengthMeters
                    - (result.FromRequiresSecondary ? (decimal)result.FromDistanceMeters : 0m)
                    - (result.ToRequiresSecondary ? (decimal)result.ToDistanceMeters : 0m)
                    + (result.FromRequiresSecondary ? 5m : 0m) + (result.ToRequiresSecondary ? 5m : 0m) + 6m;
                string expectedDesignLine = "Design length: " + decimal.Ceiling(expectedDesignBase * 1.1m).ToString("F0") + " m";
                Assert(Field<TextBox>(control, "output").Text.EndsWith(expectedDesignLine), "Design length is missing, incorrect or not the final output line.");
                Assert(ReferenceEquals(result, Field<RouteResult>(control, "result")) && session.GeometryBuildCount == designBuildCount
                    && !Field<bool>(control, "busy") && visualization.IsShown, "Design allowance edit recalculated or cleared the shown path.");
                AssertOverlayPoints(result.PathPoints);
                Invoke(control, "ReversePath");
                AssertSelection(result.SegmentIds.Select(id => session.SegmentItems[id]).ToArray());
                Assert(Field<TextBox>(control, "output").Text.EndsWith(expectedDesignLine), "Reverse changed the design length.");
                AssertOverlayPoints(result.PathPoints.Reverse().ToArray());
                AssertUiBasePoints(control, Field<RouteResult>(control, "result"), expectedTo, expectedFrom);
                Assert(Field<ModelItem>(control, "resolvedFrom").Equals(toLeaf) && Field<ModelItem>(control, "resolvedTo").Equals(fromLeaf), "Reverse did not swap resolved objects.");
                AssertColor(toLeaf, 0.10, 0.85, 0.25); AssertColor(fromLeaf, 1.00, 0.45, 0.05);
                Invoke(control, "RestoreView"); Assert(!visualization.IsShown, "Restore handler left visualization active.");
                AssertSelection(selectionBeforeShow);
                Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Restore handler left the cable overlay active.");
                AssertAppearance(before, true, true);
                Invoke(control, "ShowPath"); Assert(RoutePathOverlay.IsShownFor(document), "Restored result could not be shown again.");
                Field<TextBox>(control, "from").Text = "PF_SMOKE_MISSING_EDIT";
                Assert(Field<RouteResult>(control, "result") == null, "Changing an endpoint kept the old result.");
                Assert(RoutePathOverlay.DisplayedPoints.Count == 0, "Endpoint edit retained the previous cable overlay.");
                AssertManualMode(control);
                Assert(Field<Button>(control, "calculate").Enabled, "Endpoint editing disabled manual Calculate path.");
            }
            AssertAppearance(before, true, true);
        });
        Check("moved_endpoint_base_point_invalidates_show_and_manual_recalculation_uses_new_base", () =>
        {
            var before = Snapshot(geometry);
            var endpoints = leaves.Where(item => VisibleHeightInMeters(item) > 0.000001).Take(2).ToList();
            Assert(endpoints.Count == 2, "Sample has fewer than two visible endpoints with measurable height.");
            var routeLeaf = endpoints[0]; var movedEndpoint = endpoints[1];
            using (var originalBox = movedEndpoint.BoundingBox(true))
            {
                try
                {
                    using (var control = new PathFinderControl())
                    {
                        document.CurrentSelection.CopyFrom(new[] { routeLeaf }); Wait((Task)Invoke(control, "AssignAsync"));
                        document.CurrentSelection.CopyFrom(new[] { routeLeaf }); Invoke(control, "Pick", true);
                        document.CurrentSelection.CopyFrom(new[] { movedEndpoint }); Invoke(control, "Pick", false);
                        Wait((Task)Invoke(control, "CalculateAsync"));
                        var originalFrom = NativeBasePointInMeters(routeLeaf); var originalTo = NativeBasePointInMeters(movedEndpoint);
                        AssertUiBasePoints(control, Field<RouteResult>(control, "result"), originalFrom, originalTo);
                        Invoke(control, "ShowPath");
                        Assert(RoutePathOverlay.IsShownFor(document), "Endpoint freshness fixture did not start with a displayed cable.");
                        // Change only the endpoint. The selected route remains current,
                        // making the endpoint-base-point check responsible for rejection.
                        using (var offset = new Vector3D(0, 0, 0.125 / UnitConversion.ScaleFactor(document.Units, Units.Meters)))
                        using (var transform = Transform3D.CreateTranslation(offset))
                            document.Models.OverridePermanentTransform(new[] { movedEndpoint }, transform, false);
                        var changedTo = NativeBasePointInMeters(movedEndpoint);
                        AssertNear(changedTo.Z, originalTo.Z + 0.125);
                        var session = Field<RoutingSession>(control, "session");
                        Assert(session.AreCapturedSegmentsCurrent(), "Endpoint-only transform also changed the selected route fixture.");
                        Assert(Field<RouteResult>(control, "result") != null, "Endpoint fixture was invalidated before the Show freshness check.");
                        Invoke(control, "ShowPath");
                        Assert(Field<RouteResult>(control, "result") == null && !Field<PathVisualization>(control, "visualization").IsShown
                            && RoutePathOverlay.DisplayedPoints.Count == 0, "Show accepted a result with an outdated endpoint base point.");
                        AssertManualMode(control);
                        Wait((Task)Invoke(control, "CalculateAsync"));
                        var recalculated = Field<RouteResult>(control, "result");
                        AssertUiBasePoints(control, recalculated, NativeBasePointInMeters(routeLeaf), changedTo);
                        Invoke(control, "ShowPath"); AssertOverlayPoints(recalculated.PathPoints);
                    }
                }
                finally { document.Models.ResetPermanentTransform(new[] { movedEndpoint }); }
                using (var restoredBox = movedEndpoint.BoundingBox(true)) AssertBounds(restoredBox, originalBox);
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
        Check("secondary_threshold_manual_only_calculation_invalidation_and_reverse", () =>
        {
            var before = Snapshot(geometry);
            using (var control = new PathFinderControl())
            {
                document.CurrentSelection.CopyFrom(new[] { leaves[0] }); Wait((Task)Invoke(control, "AssignAsync"));
                var session = Field<RoutingSession>(control, "session");
                var tray = Wait(session.CaptureSegmentsAsync(CancellationToken.None)).Single();
                // Find an actual off-tray endpoint within a bounded sample subset.
                var endpoint = leaves.Where(item => !item.Equals(leaves[0]) && RoutingSession.IsVisibleEndpoint(item)).Take(128)
                    .Select(item => new { Item = item, Distance = DistanceToPolyline(NativeBasePointInMeters(item), tray.Points) })
                    .OrderByDescending(item => item.Distance).FirstOrDefault();
                Assert(endpoint != null && endpoint.Distance > 0.000001 && endpoint.Distance < 1000,
                    "Sample has no bounded visible endpoint away from the selected tray.");
                document.CurrentSelection.CopyFrom(new[] { leaves[0] }); Invoke(control, "Pick", true);
                document.CurrentSelection.CopyFrom(new[] { endpoint.Item }); Invoke(control, "Pick", false);
                var threshold = Field<NumericUpDown>(control, "secondaryDistance");
                AssertManualMode(control);
                threshold.Value = threshold.Maximum;
                Wait((Task)Invoke(control, "CalculateAsync"));
                var high = Field<RouteResult>(control, "result");
                AssertSecondaryForThreshold(high, (double)threshold.Value);
                Assert(!high.FromRequiresSecondary && !high.ToRequiresSecondary, "Large selected threshold kept a SECONDARY marker.");
                Invoke(control, "ShowPath");
                Assert(Field<PathVisualization>(control, "visualization").IsShown && RoutePathOverlay.IsShownFor(document),
                    "Threshold invalidation fixture did not start with a displayed path.");
                threshold.Value = 0m;
                Assert(Field<RouteResult>(control, "result") == null && !Field<Button>(control, "show").Enabled,
                    "Changing SECONDARY distance retained the old result.");
                Assert(!Field<PathVisualization>(control, "visualization").IsShown && RoutePathOverlay.DisplayedPoints.Count == 0,
                    "Changing SECONDARY distance retained the old view or cable overlay.");
                AssertManualMode(control);
                Assert(Field<Button>(control, "calculate").Enabled, "Threshold edit blocked manual calculation.");
                Assert(Field<Label>(control, "secondaryExplanation").Text.Contains(threshold.Value.ToString("F2")),
                    "SECONDARY explanation did not update with the selected threshold.");
                Wait((Task)Invoke(control, "CalculateAsync"));
                var low = Field<RouteResult>(control, "result");
                AssertSecondaryForThreshold(low, 0);
                Assert(low.ToRequiresSecondary && low.ToDistanceMeters > 0.000001, "Manual calculation ignored the selected zero threshold.");
                Invoke(control, "ReversePath");
                var reversed = Field<RouteResult>(control, "result");
                Assert(threshold.Value == 0m && reversed.FromRequiresSecondary == low.ToRequiresSecondary
                    && reversed.ToRequiresSecondary == low.FromRequiresSecondary, "Reverse changed the selected threshold or marker sides.");
                AssertSecondaryForThreshold(reversed, 0); AssertNear(reversed.LengthMeters, low.LengthMeters);
                threshold.Value = 1.25m;
                Wait((Task)Invoke(control, "CalculateAsync"));
                AssertSecondaryForThreshold(Field<RouteResult>(control, "result"), 1.25);
                Invoke(control, "ReversePath");
                Assert(threshold.Value == 1.25m, "Reverse did not retain a custom nonzero SECONDARY distance.");
                AssertSecondaryForThreshold(Field<RouteResult>(control, "result"), 1.25);
                AssertManualMode(control);
            }
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

    private void RotateSmokeLineToXAxis(ModelItem item, TraySegment segment)
    {
        var first = segment.Points.First(); var last = segment.Points.Last();
        using (var source = new UnitVector3D(last.X - first.X, last.Y - first.Y, last.Z - first.Z))
        using (var target = new UnitVector3D(1, 0, 0))
        using (var rotation = new Rotation3D(source, target))
        using (var transform = new Transform3D(rotation))
            document.Models.OverridePermanentTransform(new[] { item }, transform, true);
    }
    private void TranslateSmokeItemInMeters(ModelItem item, RoutePoint before, RoutePoint after)
    {
        double scale = UnitConversion.ScaleFactor(Units.Meters, document.Units);
        using (var offset = new Vector3D((after.X - before.X) * scale, (after.Y - before.Y) * scale, (after.Z - before.Z) * scale))
        using (var transform = Transform3D.CreateTranslation(offset))
            document.Models.OverridePermanentTransform(new[] { item }, transform, true);
    }
    private static RoutePoint Midpoint(RoutePoint first, RoutePoint last)
    { return new RoutePoint((first.X + last.X) * 0.5, (first.Y + last.Y) * 0.5, (first.Z + last.Z) * 0.5); }

    private RoutePoint NativeBasePointInMeters(ModelItem item)
    {
        double scale = UnitConversion.ScaleFactor(document.Units, Units.Meters);
        using (var box = item.BoundingBox(true))
        using (var minimum = box.Min)
        using (var maximum = box.Max)
        {
            Assert(!box.IsEmpty, "Endpoint fixture has no visible geometry.");
            return new RoutePoint((minimum.X + maximum.X) * 0.5 * scale,
                (minimum.Y + maximum.Y) * 0.5 * scale, minimum.Z * scale);
        }
    }

    private double VisibleHeightInMeters(ModelItem item)
    {
        if (!EffectivelyVisible(item)) return 0;
        using (var box = item.BoundingBox(true))
        using (var minimum = box.Min)
        using (var maximum = box.Max)
            return box.IsEmpty ? 0 : (maximum.Z - minimum.Z) * UnitConversion.ScaleFactor(document.Units, Units.Meters);
    }

    private static void AssertUiBasePoints(PathFinderControl control, RouteResult result, RoutePoint expectedFrom, RoutePoint expectedTo)
    {
        Assert(result != null && result.Success && result.PathPoints.Count >= 2, "Base-point UI fixture did not calculate a successful cable line.");
        AssertPoint(result.PathPoints.First(), expectedFrom); AssertPoint(result.PathPoints.Last(), expectedTo);
        AssertPoint(Field<RoutePoint>(control, "capturedFrom"), expectedFrom);
        AssertPoint(Field<RoutePoint>(control, "capturedTo"), expectedTo);
    }

    private static void AssertPoint(RoutePoint actual, RoutePoint expected)
    { AssertNear(actual.X, expected.X); AssertNear(actual.Y, expected.Y); AssertNear(actual.Z, expected.Z); }

    private static void AssertSecondaryForThreshold(RouteResult result, double threshold)
    {
        Assert(result != null && result.Success, "SECONDARY threshold fixture did not calculate a successful route.");
        Assert(result.FromRequiresSecondary == (result.FromDistanceMeters > threshold)
            && result.ToRequiresSecondary == (result.ToDistanceMeters > threshold), "SECONDARY markers did not use the selected strict distance threshold.");
        Assert((result.RouteCodes.First() == "/SECONDARY") == result.FromRequiresSecondary
            && (result.RouteCodes.Last() == "/SECONDARY") == result.ToRequiresSecondary, "Textual SECONDARY markers disagree with their endpoint flags.");
    }

    private static void AssertGapMetricsOutput(PathFinderControl control, RouteResult result)
    {
        var lines = Field<TextBox>(control, "output").Lines;
        Assert(lines.Contains("Connection gaps: " + result.ConnectionGapCount), "Rendered connection-gap count differs from the result.");
        Assert(lines.Contains("Connection gap length: " + result.ConnectionGapLengthMeters.ToString("F3") + " m"),
            "Rendered connection-gap length differs from the result.");
    }

    private static double DistanceToPolyline(RoutePoint point, IReadOnlyList<RoutePoint> line)
    {
        double minimum = double.PositiveInfinity;
        for (int index = 1; index < line.Count; index++)
        {
            var first = line[index - 1]; var last = line[index];
            double x = last.X - first.X, y = last.Y - first.Y, z = last.Z - first.Z;
            double squared = x * x + y * y + z * z;
            double fraction = squared == 0 ? 0 : ((point.X - first.X) * x + (point.Y - first.Y) * y + (point.Z - first.Z) * z) / squared;
            fraction = Math.Max(0, Math.Min(1, fraction));
            minimum = Math.Min(minimum, point.DistanceTo(new RoutePoint(first.X + x * fraction, first.Y + y * fraction, first.Z + z * fraction)));
        }
        return minimum;
    }

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
    private static void AssertBounds(BoundingBox3D actual, BoundingBox3D expected)
    {
        AssertNear(actual.Min.X, expected.Min.X); AssertNear(actual.Min.Y, expected.Min.Y); AssertNear(actual.Min.Z, expected.Min.Z);
        AssertNear(actual.Max.X, expected.Max.X); AssertNear(actual.Max.Y, expected.Max.Y); AssertNear(actual.Max.Z, expected.Max.Z);
    }
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
