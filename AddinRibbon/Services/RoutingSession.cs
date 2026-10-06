using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AddinRibbon.Routing;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Data;
using NavisworksApp = Autodesk.Navisworks.Api.Application;

namespace AddinRibbon.Services
{
    public sealed class RouteAssignment
    {
        public ModelItem Root { get; internal set; }
        public string Name { get; internal set; }
        public CableCategory Categories { get; set; }
        public int LeafCount { get; internal set; }
        internal List<ModelItem> Leaves { get; set; }
    }

    // ModelItem operations stay on the host UI thread; only value snapshots go to workers.
    public sealed class RoutingSession : IDisposable
    {
        public const string SelectedUnnamedObjectLabel = "(selected unnamed object)";
        private Document document;
        private Dictionary<string, List<ModelItem>> nameIndex;
        private readonly Dictionary<ModelItem, double[]> capturedBoxes = new Dictionary<ModelItem, double[]>();
        public List<RouteAssignment> Assignments { get; } = new List<RouteAssignment>();
        public Dictionary<string, ModelItem> SegmentItems { get; } = new Dictionary<string, ModelItem>();
        public Document Document { get { return document; } }
        public int Revision { get; private set; }
        public int ModelRevision { get; private set; }
        public event EventHandler Changed;

        public RoutingSession()
        {
            NavisworksApp.ActiveDocumentChanged += ActiveDocumentChanged;
            AttachDocument();
        }
        private void ActiveDocumentChanged(object sender, EventArgs e) { AttachDocument(); }
        private void AttachDocument()
        {
            DetachDocument();
            document = NavisworksApp.ActiveDocument;
            if (document != null)
            {
                document.Models.CollectionChanged += ModelChanged;
                document.Models.ModelTransformChanged += TransformChanged;
                document.UnitsChanged += ModelChanged;
                document.FileNameChanged += ModelChanged;
                document.FilesUpdated += ModelChanged;
            }
            InvalidateModel();
        }
        private void DetachDocument()
        {
            if (document == null) return;
            document.Models.CollectionChanged -= ModelChanged;
            document.Models.ModelTransformChanged -= TransformChanged;
            document.UnitsChanged -= ModelChanged;
            document.FileNameChanged -= ModelChanged;
            document.FilesUpdated -= ModelChanged;
        }
        private void ModelChanged(object sender, EventArgs e) { InvalidateModel(); }
        private void TransformChanged(object sender, ModelTransformEventArgs e) { InvalidateModel(); }
        private void InvalidateModel()
        {
            ModelRevision++;
            Assignments.Clear();
            nameIndex = null;
            NotifyChanged();
        }
        public void NotifyChanged()
        {
            SegmentItems.Clear();
            capturedBoxes.Clear();
            Revision++;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public async Task<int> AssignSelectionAsync(CableCategory categories, CancellationToken token)
        {
            RequireDocument();
            if (categories == CableCategory.None) throw new InvalidOperationException("Choose at least one cable type.");
            var roots = document.CurrentSelection.SelectedItems.ToList();
            if (roots.Count == 0) throw new InvalidOperationException("Select route objects in the Selection Tree first.");
            int revision = Revision, visited = 0;
            var pending = new List<RouteAssignment>();
            foreach (var root in roots)
            {
                var leaves = new List<ModelItem>();
                foreach (var item in root.DescendantsAndSelf)
                {
                    token.ThrowIfCancellationRequested();
                    if (!item.Children.Any() && item.HasGeometry && !item.BoundingBox().IsEmpty) leaves.Add(item);
                    if (++visited % 200 == 0) await Task.Delay(1, token);
                    if (revision != Revision) throw new OperationCanceledException("Model changed during route capture.");
                }
                if (leaves.Count > 0)
                    pending.Add(new RouteAssignment { Root = root, Name = root.DisplayName, Categories = categories, LeafCount = leaves.Count, Leaves = leaves });
            }
            if (pending.Count == 0) throw new InvalidOperationException("The selection has no deepest children with geometry.");
            foreach (var assignment in pending)
            {
                Assignments.RemoveAll(a => a.Root.Equals(assignment.Root));
                Assignments.Add(assignment);
            }
            NotifyChanged();
            return pending.Sum(a => a.LeafCount);
        }

        public async Task<ModelItem> ResolveAsync(string text, ModelItem picked, CancellationToken token)
        {
            RequireDocument();
            string name = (text ?? "").Trim();
            if (name.Length == 0) throw new InvalidOperationException("Enter both From and To object names.");
            if (picked != null && !picked.IsDisposed)
            {
                string pickedName = (picked.DisplayName ?? "").Trim();
                if (string.Equals(pickedName, name, StringComparison.OrdinalIgnoreCase)
                    || (pickedName.Length == 0 && name == SelectedUnnamedObjectLabel)) return picked;
            }
            if (nameIndex == null)
            {
                int revision = Revision, count = 0;
                var index = new Dictionary<string, List<ModelItem>>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in document.Models.CreateCollectionFromRootItems().DescendantsAndSelf)
                {
                    token.ThrowIfCancellationRequested();
                    if (!string.IsNullOrWhiteSpace(item.DisplayName))
                    {
                        if (!index.TryGetValue(item.DisplayName.Trim(), out var list)) index[item.DisplayName.Trim()] = list = new List<ModelItem>();
                        list.Add(item);
                    }
                    if (++count % 300 == 0) await Task.Delay(1, token);
                    if (revision != Revision) throw new OperationCanceledException("Model changed during object search.");
                }
                nameIndex = index;
            }
            if (!nameIndex.TryGetValue(name, out var matches)) throw new InvalidOperationException("Object not found: " + name);
            if (matches.Count != 1) throw new InvalidOperationException("More than one object is named " + name + ". Select the intended object and use 'Use selection'.");
            return matches[0];
        }

        public RoutePoint CenterInMeters(ModelItem item)
        {
            var box = item.BoundingBox();
            if (box.IsEmpty) throw new InvalidOperationException("Object has no geometry: " + item.DisplayName);
            return ToMeters(box.Center, UnitConversion.ScaleFactor(document.Units, Units.Meters));
        }

        public async Task<List<TraySegment>> CaptureSegmentsAsync(CancellationToken token)
        {
            RequireDocument();
            if (Assignments.Count == 0) throw new InvalidOperationException("Add route objects on the Routes tab first.");
            int revision = Revision;
            // The most recently added rule wins when selections overlap.
            var assigned = new Dictionary<ModelItem, RouteAssignment>();
            foreach (var assignment in Assignments)
                foreach (var leaf in assignment.Leaves) assigned[leaf] = assignment;
            double scale = UnitConversion.ScaleFactor(document.Units, Units.Meters);
            var segments = new List<TraySegment>();
            var items = new Dictionary<string, ModelItem>();
            var boxes = new Dictionary<ModelItem, double[]>();
            int count = 0, visited = 0;
            foreach (var pair in assigned)
            {
                token.ThrowIfCancellationRequested();
                var item = pair.Key;
                var box = item.BoundingBox();
                if (pair.Value.Categories != CableCategory.None && !box.IsEmpty)
                {
                    string id = (++count).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    segments.Add(new TraySegment(id, FindRouteName(item, pair.Value.Root), pair.Value.Categories, Centerline(box, scale)));
                    items.Add(id, item);
                    boxes.Add(item, BoxCoordinates(box));
                }
                if (++visited % 200 == 0) await Task.Delay(1, token);
                if (revision != Revision) throw new OperationCanceledException("Model changed during geometry capture.");
            }
            SegmentItems.Clear();
            foreach (var pair in items) SegmentItems.Add(pair.Key, pair.Value);
            capturedBoxes.Clear();
            foreach (var pair in boxes) capturedBoxes.Add(pair.Key, pair.Value);
            return segments;
        }

        // Item-level Transform overrides do not raise ModelTransformChanged in the host.
        // Recheck the captured geometry before displaying a cached result.
        public bool AreCapturedSegmentsCurrent()
        {
            if (capturedBoxes.Count == 0) return false;
            foreach (var pair in capturedBoxes)
            {
                if (pair.Key.IsDisposed) return false;
                var box = pair.Key.BoundingBox();
                if (box.IsEmpty || !pair.Value.SequenceEqual(BoxCoordinates(box))) return false;
            }
            return true;
        }
        private static double[] BoxCoordinates(BoundingBox3D box)
        {
            return new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z };
        }

        private static string FindRouteName(ModelItem leaf, ModelItem root)
        {
            foreach (var item in leaf.AncestorsAndSelf)
            {
                if (RouteCodeParser.TryParseNamedCode(item.DisplayName, out var code)) return code;
                if (item.Equals(root)) break;
            }
            return !string.IsNullOrWhiteSpace(root.DisplayName) ? root.DisplayName : leaf.DisplayName;
        }
        // PoC centreline approximation; CAD curves for elbows/rotated parts are not extracted.
        private static IEnumerable<RoutePoint> Centerline(BoundingBox3D box, double scale)
        {
            var c = box.Center; var size = box.Size;
            if (size.X >= size.Y && size.X >= size.Z)
                return new[] { ToMeters(new Point3D(box.Min.X, c.Y, c.Z), scale), ToMeters(new Point3D(box.Max.X, c.Y, c.Z), scale) };
            if (size.Y >= size.Z)
                return new[] { ToMeters(new Point3D(c.X, box.Min.Y, c.Z), scale), ToMeters(new Point3D(c.X, box.Max.Y, c.Z), scale) };
            return new[] { ToMeters(new Point3D(c.X, c.Y, box.Min.Z), scale), ToMeters(new Point3D(c.X, c.Y, box.Max.Z), scale) };
        }
        private static RoutePoint ToMeters(Point3D p, double scale) { return new RoutePoint(p.X * scale, p.Y * scale, p.Z * scale); }
        private void RequireDocument()
        {
            if (document == null || document.Models.Count == 0) throw new InvalidOperationException("Open a 3D model in Navisworks first.");
        }
        public void Dispose()
        {
            NavisworksApp.ActiveDocumentChanged -= ActiveDocumentChanged;
            DetachDocument();
        }
    }
}
