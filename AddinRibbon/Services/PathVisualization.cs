using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;

namespace AddinRibbon.Services
{
    /// <summary>
    /// A temporary view of one route. Background transparency resets to zero rather than its previous value.
    /// All Navisworks calls stay on the creating UI thread.
    /// Permanent materials and the original hidden state are never changed.
    /// </summary>
    public sealed class PathVisualization : IDisposable
    {
        private readonly int _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        private Document _document;
        private List<MaterialSnapshot> _materials;
        private List<ModelItem> _selectionBefore;
        private HashSet<ModelItem> _selectedPath;
        private List<ModelItem> _protectedRoots;
        private HashSet<ModelItem> _protectedGeometry;
        private BackgroundSnapshot _background;
        private CancellationTokenSource _backgroundOperation;
        private int _viewGeneration;
        private bool _disposed;

        private const int BackgroundRootBatchSize = 4096;

        public bool IsShown { get { return _materials != null; } }
        public bool HasBackgroundTransparency { get { return _background != null && _background.Mutated; } }
        public double? AppliedBackgroundTransparency
        { get { return HasBackgroundTransparency && _background.ValueIsComplete ? (double?)_background.Transparency : null; } }
        public bool IsPathSelected
        {
            get
            {
                VerifyThread();
                if (_document == null || _materials == null || _selectedPath == null || _selectedPath.Count == 0
                    || _document.IsDisposed || _document.IsClear) return false;
                try { return _selectedPath.SetEquals(_document.CurrentSelection.SelectedItems); }
                catch (ObjectDisposedException) { return false; }
                catch (ArgumentException) { return false; }
            }
        }

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

            // Validate document membership before changing appearance or selection.
            document.Models.CreatePathId(from);
            document.Models.CreatePathId(to);
            var route = pathLeaves.Where(item => item != null).Distinct().ToList();
            foreach (var item in route) document.Models.CreatePathId(item);

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

            var snapshots = highlighted.Select(Capture).ToList();
            _document = document;
            _materials = snapshots;
            _selectionBefore = document.CurrentSelection.SelectedItems.ToList();
            _selectedPath = new HashSet<ModelItem>(route);
            _protectedRoots = route.Concat(new[] { from, to }).Distinct().ToList();
            _protectedGeometry = highlighted;
            try
            {
                // Change only the route and endpoints; other objects retain their appearance.
                document.Models.OverrideTemporaryTransparency(highlighted, 0.0);
                SetColor(document, routeGeometry, 0.10, 0.65, 1.00);
                fromGeometry.ExceptWith(commonGeometry);
                toGeometry.ExceptWith(commonGeometry);
                SetColor(document, fromGeometry, 0.10, 0.85, 0.25);
                SetColor(document, toGeometry, 1.00, 0.45, 0.05);
                SetColor(document, commonGeometry, 0.75, 0.20, 1.00);
                document.CurrentSelection.CopyFrom(route);
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
            CancelBackgroundOperation();
            _viewGeneration++;
            var document = _document;
            var materials = _materials;
            var selectionBefore = _selectionBefore;
            var selectedPath = _selectedPath;
            var background = _background;
            _document = null;
            _materials = null;
            _selectionBefore = null;
            _selectedPath = null;
            _protectedRoots = null;
            _protectedGeometry = null;
            _background = null;
            if (document == null || materials == null || document.IsDisposed) return;

            // Removed models can invalidate captured native items. Never resolve an old
            // index path against a new model; it could point at a different object.
            var live = materials.Where(snapshot => !snapshot.Item.IsDisposed).ToList();
            try
            {
                // Full Restore synchronously resets only compact background roots to zero.
                // Explicit background Restore yields before its native overrides.
                if (background != null && background.Mutated && !document.IsClear)
                    RestoreBackgroundSynchronously(document, background);
                if (live.Count > 0)
                {
                    document.Models.ResetTemporaryMaterials(live.Select(snapshot => snapshot.Item));
                    RestorePreviousTemporaryAppearance(document, live);
                }
            }
            catch (ObjectDisposedException)
            {
                // The document/model was closed while its pane was being destroyed.
            }
            catch (ArgumentException)
            {
                // Collection replacement can remove items before its change event.
            }
            finally
            {
                RestoreSelection(document, selectionBefore, selectedPath);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Restore();
            _disposed = true;
        }

        public Task<int> ApplyBackgroundTransparencyAsync(CancellationToken token, Action<int> progress = null)
        {
            return ApplyBackgroundTransparencyAsync(0.75, token, progress);
        }

        public async Task<int> ApplyBackgroundTransparencyAsync(double transparency, CancellationToken token, Action<int> progress = null)
        {
            VerifyThread();
            if (_disposed) throw new ObjectDisposedException(nameof(PathVisualization));
            if (double.IsNaN(transparency) || double.IsInfinity(transparency) || transparency < 0.0 || transparency > 1.0)
                throw new ArgumentOutOfRangeException(nameof(transparency), "Transparency must be a finite value between zero and one.");
            token.ThrowIfCancellationRequested();
            if (!IsPathSelected) throw new InvalidOperationException("Show the path and keep its route objects selected first.");
            if (HasBackgroundTransparency && _background.ValueIsComplete && _background.Transparency == transparency)
                return _background.Roots.Count;
            var operation = BeginBackgroundOperation(token);
            var owner = new BackgroundOwner(_document, _materials, _viewGeneration);
            var previous = _background;
            BackgroundSnapshot captured = previous;
            bool mutated = false;
            try
            {
                // Let the UI paint the operation status and process cancellation first.
                progress?.Invoke(0);
                await Task.Delay(1, operation.Token);
                EnsureBackgroundOwner(owner, operation.Token, true);
                if (captured == null) captured = CaptureBackgroundRoots(owner, operation.Token);
                if (captured.Roots.Count == 0) return 0;
                _background = captured;
                await ProcessBackgroundAsync(owner, captured, false, transparency, operation.Token, progress, true,
                    () => mutated = true);
                captured.ValueIsComplete = true;
                if (transparency == 0.0 && ReferenceEquals(_background, captured)) _background = null;
                return captured.Roots.Count;
            }
            catch
            {
                if (captured != null && ReferenceEquals(_background, captured))
                {
                    if (mutated)
                    {
                        // Reset attempted background overrides to zero even after cancellation.
                        // A new view or disposed model invalidates the owner and stops cleanup.
                        if (IsBackgroundOwnerCurrent(owner))
                            await ProcessBackgroundAsync(owner, captured, true, 0.0, CancellationToken.None, null, false);
                        if (ReferenceEquals(_background, captured)) _background = null;
                    }
                    else if (ReferenceEquals(_background, captured))
                    {
                        // An existing value remains active when canceled before a native mutation.
                        _background = previous;
                    }
                }
                throw;
            }
            finally { EndBackgroundOperation(operation); }
        }

        public async Task RestoreBackgroundTransparencyAsync(CancellationToken token, Action<int> progress = null)
        {
            VerifyThread();
            token.ThrowIfCancellationRequested();
            var captured = _background;
            if (captured == null) return;
            var operation = BeginBackgroundOperation(token);
            var owner = new BackgroundOwner(_document, _materials, _viewGeneration);
            try
            {
                // Cancellation retains the compact root mask so Restore can be retried.
                // Already reset roots are safe to reset to zero again.
                progress?.Invoke(0);
                await Task.Delay(1, operation.Token);
                EnsureBackgroundOwner(owner, operation.Token, false);
                await ProcessBackgroundAsync(owner, captured, true, 0.0, operation.Token, progress, false);
                EnsureBackgroundOwner(owner, operation.Token, false);
                if (ReferenceEquals(_background, captured)) _background = null;
            }
            finally { EndBackgroundOperation(operation); }
        }

        private BackgroundSnapshot CaptureBackgroundRoots(BackgroundOwner owner, CancellationToken token)
        {
            using (var inverse = new ModelItemCollection())
            {
                EnsureBackgroundOwner(owner, token, true);
                inverse.CopyFrom(_protectedRoots);
                // Invert/compression use a detached collection and never change CurrentSelection.
                // Each native call is synchronous; cancellation is checked on both sides.
                EnsureBackgroundOwner(owner, token, true);
                inverse.Invert(owner.Document);
                EnsureBackgroundOwner(owner, token, true);
                inverse.MakeDisjoint();
                EnsureBackgroundOwner(owner, token, true);
                inverse.Minimize();
                EnsureBackgroundOwner(owner, token, true);
                var captured = new BackgroundSnapshot(inverse.Where(item => item != null && !item.IsDisposed).Distinct().ToList());
                EnsureBackgroundOwner(owner, token, true);
                return captured;
            }
        }

        private async Task ProcessBackgroundAsync(BackgroundOwner owner, BackgroundSnapshot captured, bool restoring,
            double transparency, CancellationToken token, Action<int> progress, bool requireSelection, Action mutationStarted = null)
        {
            int count = restoring ? captured.TouchedCount : captured.Roots.Count;
            for (int index = 0; index < count; index += BackgroundRootBatchSize)
            {
                EnsureBackgroundOwner(owner, token, requireSelection);
                int batchCount = Math.Min(BackgroundRootBatchSize, count - index);
                var roots = captured.Roots.GetRange(index, batchCount);
                var foreground = CaptureForegroundColors(owner);
                EnsureBackgroundOwner(owner, token, requireSelection);
                // A canceled reset/reapply can leave mixed alpha; expose no single applied value.
                captured.ValueIsComplete = false;
                if (!restoring)
                {
                    // Include a possibly partially executed native call in cancellation cleanup.
                    // Reapplying retains older affected roots too, so rollback cannot leave old alpha behind.
                    mutationStarted?.Invoke();
                    captured.TouchedCount = Math.Max(captured.TouchedCount, index + batchCount);
                    captured.Mutated = true;
                    captured.Transparency = transparency;
                }
                try { OverrideTransparencySafely(owner.Document, roots, transparency); }
                finally
                {
                    // Native transparency overrides can coalesce existing temporary colors.
                    // Restore only protected foreground RGB, before yielding or cancellation.
                    RestoreForegroundAppearance(owner, foreground);
                }
                EnsureBackgroundOwner(owner, token, requireSelection);
                progress?.Invoke(index + batchCount);
                EnsureBackgroundOwner(owner, token, requireSelection);
                if (index + batchCount < count)
                {
                    // Native subtree overrides cannot be interrupted; yield between root batches.
                    await Task.Delay(1, token);
                    EnsureBackgroundOwner(owner, token, requireSelection);
                }
            }
            EnsureBackgroundOwner(owner, token, requireSelection);
        }

        private static List<ForegroundColorSnapshot> CaptureForegroundColors(BackgroundOwner owner)
        {
            var snapshots = new List<ForegroundColorSnapshot>(owner.Materials.Count);
            foreach (var material in owner.Materials)
            {
                if (material.Item.IsDisposed) continue;
                using (var color = material.Item.Geometry.ActiveColor)
                    snapshots.Add(new ForegroundColorSnapshot(material.Item, material.Depth,
                        Tuple.Create(color.R, color.G, color.B)));
            }
            return snapshots;
        }

        private void RestoreForegroundAppearance(BackgroundOwner owner, List<ForegroundColorSnapshot> snapshots)
        {
            if (!IsBackgroundOwnerCurrent(owner)) return;
            // Keep all route/endpoints opaque even if the inverse includes a geometry ancestor.
            OverrideTransparencySafely(owner.Document, _protectedGeometry, 0.0);
            // Parent overrides affect descendants: restore parents before their children.
            foreach (var depth in snapshots.GroupBy(snapshot => snapshot.Depth).OrderBy(group => group.Key))
            {
                foreach (var group in depth.GroupBy(snapshot => snapshot.Color))
                {
                    if (!IsBackgroundOwnerCurrent(owner)) return;
                    using (var color = new Color(group.Key.Item1, group.Key.Item2, group.Key.Item3))
                        OverrideForegroundColorSafely(owner, group.Select(snapshot => snapshot.Item), color);
                }
            }
        }

        private void OverrideForegroundColorSafely(BackgroundOwner owner, IEnumerable<ModelItem> items, Color color)
        {
            var live = items.Where(item => item != null && !item.IsDisposed).ToList();
            if (live.Count == 0 || !IsBackgroundOwnerCurrent(owner)) return;
            try { owner.Document.Models.OverrideTemporaryColor(live, color); return; }
            catch (ObjectDisposedException) { }
            catch (ArgumentException) { }
            foreach (var item in live)
            {
                if (!IsBackgroundOwnerCurrent(owner)) return;
                try
                {
                    if (item.IsDisposed) continue;
                    owner.Document.Models.CreatePathId(item);
                    owner.Document.Models.OverrideTemporaryColor(new[] { item }, color);
                }
                catch (ObjectDisposedException) { }
                catch (ArgumentException) { }
            }
        }

        private static void RestoreBackgroundSynchronously(Document document, BackgroundSnapshot captured)
        {
            for (int index = 0; index < captured.TouchedCount; index += BackgroundRootBatchSize)
            {
                int count = Math.Min(BackgroundRootBatchSize, captured.TouchedCount - index);
                OverrideTransparencySafely(document, captured.Roots.GetRange(index, count), 0.0);
            }
        }

        private static void OverrideTransparencySafely(Document document, IEnumerable<ModelItem> items, double value)
        {
            var live = items.Where(item => item != null && !item.IsDisposed).ToList();
            if (live.Count == 0 || document.IsDisposed || document.IsClear) return;
            try { document.Models.OverrideTemporaryTransparency(live, value); }
            catch (ObjectDisposedException) { ReplayLiveTransparency(document, live, value); }
            catch (ArgumentException) { ReplayLiveTransparency(document, live, value); }
        }

        private static void ReplayLiveTransparency(Document document, IEnumerable<ModelItem> items, double value)
        {
            foreach (var item in items)
            {
                if (document.IsDisposed || document.IsClear) return;
                try
                {
                    if (item.IsDisposed) continue;
                    document.Models.CreatePathId(item);
                    document.Models.OverrideTemporaryTransparency(new[] { item }, value);
                }
                catch (ObjectDisposedException) { }
                catch (ArgumentException) { }
            }
        }

        private CancellationTokenSource BeginBackgroundOperation(CancellationToken token)
        {
            if (_backgroundOperation != null) throw new InvalidOperationException("A background transparency operation is already running.");
            return _backgroundOperation = CancellationTokenSource.CreateLinkedTokenSource(token);
        }

        private void EndBackgroundOperation(CancellationTokenSource operation)
        {
            if (ReferenceEquals(_backgroundOperation, operation)) _backgroundOperation = null;
            operation.Dispose();
        }

        private void CancelBackgroundOperation()
        {
            var operation = _backgroundOperation;
            _backgroundOperation = null;
            if (operation != null) operation.Cancel();
        }

        private bool IsBackgroundOwnerCurrent(BackgroundOwner owner)
        {
            return !_disposed && owner.Document != null && !owner.Document.IsDisposed && !owner.Document.IsClear
                && ReferenceEquals(_document, owner.Document) && ReferenceEquals(_materials, owner.Materials)
                && _viewGeneration == owner.Generation;
        }

        private void EnsureBackgroundOwner(BackgroundOwner owner, CancellationToken token, bool requireSelection)
        {
            VerifyThread(); token.ThrowIfCancellationRequested();
            if (!IsBackgroundOwnerCurrent(owner) || (requireSelection && !IsPathSelected))
                throw new OperationCanceledException("The shown path, document or route selection changed.");
        }

        private static HashSet<ModelItem> GeometryOf(IEnumerable<ModelItem> items)
        {
            return new HashSet<ModelItem>(items.SelectMany(item => item.DescendantsAndSelf)
                .Where(item => item.HasGeometry));
        }

        private static void RestoreSelection(Document document, List<ModelItem> previous, HashSet<ModelItem> path)
        {
            if (previous == null || path == null || document.IsDisposed || document.IsClear) return;
            try
            {
                // A later manual selection belongs to the user, not this temporary view.
                if (!path.SetEquals(document.CurrentSelection.SelectedItems)) return;
                var live = new List<ModelItem>();
                foreach (var item in previous)
                {
                    if (item.IsDisposed) continue;
                    try { document.Models.CreatePathId(item); live.Add(item); }
                    catch (ObjectDisposedException) { }
                    catch (ArgumentException) { }
                }
                document.CurrentSelection.CopyFrom(live);
            }
            catch (ObjectDisposedException) { }
            catch (ArgumentException) { }
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
            // Alpha replay can replace temporary RGB. Restore the captured color of every
            // affected item, including items whose previous RGB came from permanent materials.
            restoreColors.UnionWith(restoreTransparencies);

            // Overrides also affect descendants. Replay shallower geometry first and
            // then correct its children, including children with original materials.
            foreach (var depth in snapshots.Select(snapshot => snapshot.Depth).Distinct().OrderBy(value => value))
            {
                foreach (var group in restoreTransparencies.Where(snapshot => snapshot.Depth == depth).GroupBy(snapshot => snapshot.Transparency))
                    document.Models.OverrideTemporaryTransparency(group.Select(snapshot => snapshot.Item), group.Key);
                foreach (var group in restoreColors.Where(snapshot => snapshot.Depth == depth).GroupBy(snapshot => snapshot.Color))
                {
                    using (var color = new Color(group.Key.Item1, group.Key.Item2, group.Key.Item3))
                        document.Models.OverrideTemporaryColor(group.Select(snapshot => snapshot.Item), color);
                }
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

        private sealed class BackgroundOwner
        {
            public readonly Document Document;
            public readonly List<MaterialSnapshot> Materials;
            public readonly int Generation;
            public BackgroundOwner(Document document, List<MaterialSnapshot> materials, int generation)
            { Document = document; Materials = materials; Generation = generation; }
        }

        private sealed class BackgroundSnapshot
        {
            public readonly List<ModelItem> Roots;
            public int TouchedCount;
            public bool Mutated;
            public double Transparency;
            public bool ValueIsComplete;
            public BackgroundSnapshot(List<ModelItem> roots) { Roots = roots; }
        }

        private sealed class ForegroundColorSnapshot
        {
            public readonly ModelItem Item;
            public readonly int Depth;
            public readonly Tuple<double, double, double> Color;
            public ForegroundColorSnapshot(ModelItem item, int depth, Tuple<double, double, double> color)
            { Item = item; Depth = depth; Color = color; }
        }
    }
}
