using System;
using System.Collections.Generic;
using System.Linq;
using AddinRibbon.Routing;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace AddinRibbon.Services
{
    // A temporary, world-anchored cable line. It never creates or saves model geometry.
    [Plugin("PathFinderRouteOverlay", "CONN", DisplayName = "Path Finder cable line")]
    public sealed class RoutePathOverlay : RenderPlugin
    {
        private sealed class ShownPath
        {
            public readonly Document Document;
            public readonly RoutePoint[] Points;
            public ShownPath(Document document, RoutePoint[] points) { Document = document; Points = points; }
        }
        private static volatile ShownPath shownPath;

        public static IReadOnlyList<RoutePoint> DisplayedPoints { get { return Array.AsReadOnly(shownPath?.Points ?? new RoutePoint[0]); } }
        public static bool IsShownFor(Document document)
        {
            return Matches(shownPath, document);
        }
        private static bool Matches(ShownPath path, Document document)
        {
            return path != null && path.Document.Equals(document) && path.Points.Length > 1;
        }

        public static void Show(Document document, IEnumerable<RoutePoint> pointsInMeters)
        {
            if (document == null || document.IsDisposed) throw new InvalidOperationException("Open a model before showing a cable line.");
            if (pointsInMeters == null) throw new ArgumentNullException(nameof(pointsInMeters));
            var record = Application.Plugins.FindPlugin("PathFinderRouteOverlay.CONN") as RenderPluginRecord;
            if (record == null || !record.IsEnabled) throw new InvalidOperationException("The Path Finder cable-line renderer is unavailable.");
            if (record.LoadedPlugin == null) record.LoadPlugin();
            double scale = UnitConversion.ScaleFactor(Units.Meters, document.Units);
            var points = pointsInMeters.Select(point => new RoutePoint(point.X * scale, point.Y * scale, point.Z * scale)).ToArray();
            shownPath = new ShownPath(document, points);
            RequestRedraw(document);
        }

        public static void Clear()
        {
            var document = shownPath?.Document;
            shownPath = null;
            RequestRedraw(document);
        }

        private static void RequestRedraw(Document document)
        {
            if (document != null && !document.IsDisposed) document.ActiveView?.RequestDelayedRedraw(ViewRedrawRequests.All);
        }

        public override BoundingBox3D MakeRenderBoundingBox(View view)
        {
            var path = shownPath;
            if (!Matches(path, view.Document)) return new BoundingBox3D();
            using (var minimum = new Point3D(path.Points.Min(point => point.X), path.Points.Min(point => point.Y), path.Points.Min(point => point.Z)))
            using (var maximum = new Point3D(path.Points.Max(point => point.X), path.Points.Max(point => point.Y), path.Points.Max(point => point.Z)))
                return new BoundingBox3D(minimum, maximum);
        }

        public override void OverlayRender(View view, Graphics graphics)
        {
            var path = shownPath;
            if (!Matches(path, view.Document)) return;
            using (var line = new Point3DList())
            using (var color = new Color(1.0, 0.9, 0.05))
            {
                foreach (var point in path.Points)
                    using (var native = new Point3D(point.X, point.Y, point.Z)) line.Add(native);
                graphics.BeginModelContext();
                try
                {
                    graphics.DepthTest(false);
                    graphics.DepthMask(false);
                    graphics.Lighting(false);
                    graphics.Color(color, 1.0);
                    graphics.LineWidth(3.0);
                    graphics.Polyline3D(line);
                }
                finally { graphics.EndModelContext(); }
            }
        }

        protected override void OnUnloading() { Clear(); base.OnUnloading(); }
    }
}
