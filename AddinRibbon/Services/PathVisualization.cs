using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;

namespace AddinRibbon.Services
{
    /// <summary>
    /// A reversible view of one route. All Navisworks calls stay on the creating UI thread.
    /// Permanent materials and the original hidden state are never changed.
    /// </summary>
    public sealed class PathVisualization : IDisposable
    {
        private readonly int _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        private Document _document;
        private List<MaterialSnapshot> _materials;
        private bool _disposed;

        public bool IsShown { get { return _materials != null; } }

        public void Show(Document document, IEnumerable<ModelItem> pathLeaves, ModelItem from, ModelItem to)
        {
            VerifyThread();
            if (_disposed) throw new ObjectDisposedException(nameof(PathVisualization));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.IsDisposed || document.IsClear)
                throw new InvalidOperationException("Open a model before showing a path.");
            if (pathLeaves == null) throw new ArgumentNullException(nameof(pathLeaves));
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            Restore();

            // Resolve all data before the first override, so an invalid endpoint cannot
            // leave the whole document transparent. These index paths also validate
            // that the supplied endpoints belong to this document.
            document.Models.CreatePathId(from);
            document.Models.CreatePathId(to);
            var route = pathLeaves.Where(item => item != null).Distinct().ToList();
            foreach (var item in route) document.Models.CreatePathId(item);

            var rootItems = document.Models.CreateCollectionFromRootItems();
            var geometryItems = rootItems.DescendantsAndSelf.Where(item => item.HasGeometry).Distinct().ToList();
            var routeGeometry = GeometryOf(route);
            var fromGeometry = GeometryOf(new[] { from });
            var toGeometry = GeometryOf(new[] { to });
            var commonGeometry = new HashSet<ModelItem>(fromGeometry);
            commonGeometry.IntersectWith(toGeometry);
            var highlighted = new HashSet<ModelItem>(routeGeometry);
            highlighted.UnionWith(fromGeometry);
            highlighted.UnionWith(toGeometry);
            if (highlighted.Count == 0)
                throw new InvalidOperationException("The selected objects and path have no displayable geometry.");

            var snapshots = geometryItems.Select(Capture).ToList();
            _document = document;
            _materials = snapshots;
            try
            {
                // Bulk native operations avoid one override call for every model object.
                document.Models.OverrideTemporaryTransparency(rootItems, 0.95);
                document.Models.OverrideTemporaryTransparency(highlighted, 0.0);
                SetColor(document, routeGeometry, 0.10, 0.65, 1.00);
                fromGeometry.ExceptWith(commonGeometry);
                toGeometry.ExceptWith(commonGeometry);
                SetColor(document, fromGeometry, 0.10, 0.85, 0.25);
                SetColor(document, toGeometry, 1.00, 0.45, 0.05);
                SetColor(document, commonGeometry, 0.75, 0.20, 1.00);
            }
            catch
            {
                Restore();
                throw;
            }
        }

        public void Restore()
        {
            VerifyThread();
            var document = _document;
            var materials = _materials;
            _document = null;
            _materials = null;
            if (document == null || materials == null || document.IsDisposed) return;

            // Removed models can invalidate captured native items. Never resolve an old
            // index path against a new model; it could point at a different object.
            var live = materials.Where(snapshot => !snapshot.Item.IsDisposed).ToList();
            if (live.Count == 0) return;
            try
            {
                document.Models.ResetTemporaryMaterials(live.Select(snapshot => snapshot.Item));
                RestorePreviousTemporaryAppearance(document, live);
            }
            catch (ObjectDisposedException)
            {
                // The document/model was closed while its pane was being destroyed.
            }
            catch (ArgumentException)
            {
                // Collection replacement can remove items before its change event.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Restore();
            _disposed = true;
        }

        private static HashSet<ModelItem> GeometryOf(IEnumerable<ModelItem> items)
        {
            return new HashSet<ModelItem>(items.SelectMany(item => item.DescendantsAndSelf)
                .Where(item => item.HasGeometry));
        }

        private static MaterialSnapshot Capture(ModelItem item)
        {
            var geometry = item.Geometry;
            var active = geometry.ActiveColor;
            var permanent = geometry.PermanentColor;
            var activeColor = Tuple.Create(active.R, active.G, active.B);
            var permanentColor = Tuple.Create(permanent.R, permanent.G, permanent.B);
            var transparency = geometry.ActiveTransparency;
            return new MaterialSnapshot
            {
                Item = item,
                Depth = item.Ancestors.Count(),
                Color = activeColor,
                Transparency = transparency,
                HadTemporaryColor = !activeColor.Equals(permanentColor),
                HadTemporaryTransparency = transparency != geometry.PermanentTransparency
            };
        }

        private static void RestorePreviousTemporaryAppearance(Document document, List<MaterialSnapshot> snapshots)
        {
            var byItem = snapshots.ToDictionary(snapshot => snapshot.Item);
            var restoreColors = PreviouslyAffected(snapshots, snapshot => snapshot.HadTemporaryColor, byItem);
            var restoreTransparencies = PreviouslyAffected(snapshots, snapshot => snapshot.HadTemporaryTransparency, byItem);

            // Overrides also affect descendants. Replay shallower geometry first and
            // then correct its children, including children with original materials.
            foreach (var depth in snapshots.Select(snapshot => snapshot.Depth).Distinct().OrderBy(value => value))
            {
                foreach (var group in restoreColors.Where(snapshot => snapshot.Depth == depth).GroupBy(snapshot => snapshot.Color))
                {
                    using (var color = new Color(group.Key.Item1, group.Key.Item2, group.Key.Item3))
                        document.Models.OverrideTemporaryColor(group.Select(snapshot => snapshot.Item), color);
                }
                foreach (var group in restoreTransparencies.Where(snapshot => snapshot.Depth == depth).GroupBy(snapshot => snapshot.Transparency))
                    document.Models.OverrideTemporaryTransparency(group.Select(snapshot => snapshot.Item), group.Key);
            }
        }

        private static HashSet<MaterialSnapshot> PreviouslyAffected(List<MaterialSnapshot> snapshots,
            Func<MaterialSnapshot, bool> hadOverride, Dictionary<ModelItem, MaterialSnapshot> byItem)
        {
            var affected = new HashSet<MaterialSnapshot>();
            foreach (var snapshot in snapshots.Where(hadOverride))
            {
                affected.Add(snapshot);
                foreach (var descendant in snapshot.Item.Descendants)
                {
                    MaterialSnapshot child;
                    if (byItem.TryGetValue(descendant, out child)) affected.Add(child);
                }
            }
            return affected;
        }

        private static void SetColor(Document document, ICollection<ModelItem> items, double red, double green, double blue)
        {
            if (items.Count == 0) return;
            using (var color = new Color(red, green, blue))
                document.Models.OverrideTemporaryColor(items, color);
        }

        private void VerifyThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _uiThreadId)
                throw new InvalidOperationException("Path visualization must run on the Navisworks UI thread.");
        }

        private sealed class MaterialSnapshot
        {
            public ModelItem Item;
            public int Depth;
            public Tuple<double, double, double> Color;
            public double Transparency;
            public bool HadTemporaryColor;
            public bool HadTemporaryTransparency;
        }
    }
}
