using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AddinRibbon.Routing;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using COM = Autodesk.Navisworks.Api.Interop.ComApi;

namespace AddinRibbon.Services
{
    // Read only positively identified bends, in the host thread. Keep constant-size
    // face statistics rather than mesh vertices or native geometry on the worker.
    internal static class TrayMeshReader
    {
        private const long MaximumPrimitives = 50000;
        private const int MaximumFragments = 64;
        private const double FaceBandMeters = 0.001;

        public static double[] ActiveTransform(ModelItem item)
        {
            using (var geometry = item.Geometry)
            using (var transform = geometry.ActiveTransform)
            using (var linear = transform.Linear)
            using (var translation = transform.Translation)
            {
                var values = new double[12];
                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++) values[row * 3 + column] = linear.Get(row, column);
                values[9] = translation.X; values[10] = translation.Y; values[11] = translation.Z;
                return values;
            }
        }

        public static IReadOnlyList<TrayFaceExtent> ReadFaces(ModelItem item, RoutePoint min, RoutePoint max,
            double metresPerUnit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var geometry = item.Geometry)
                if (geometry.PrimitiveCount > MaximumPrimitives) return new TrayFaceExtent[0];
            var faces = new FaceAccumulator(min, max, token);
            var path = ComApiBridge.ToInwOaPath(item);
            int fragments = 0;
            foreach (COM.InwOaFragment3 fragment in path.Fragments())
            {
                token.ThrowIfCancellationRequested();
                if (++fragments > MaximumFragments) return new TrayFaceExtent[0];
                var matrix = ReadArray(fragment.GetLocalToWorldMatrix().Matrix, 16);
                fragment.GenerateSimplePrimitives(COM.nwEVertexProperty.eNONE, new PrimitiveReader(faces, matrix, metresPerUnit));
            }
            token.ThrowIfCancellationRequested();
            return faces.Result();
        }

        private static double[] ReadArray(object value, int expected)
        {
            var array = value as Array;
            if (array == null || array.Rank != 1 || array.Length != expected)
                throw new InvalidOperationException("Unexpected native mesh coordinate array.");
            int start = array.GetLowerBound(0);
            var values = new double[expected];
            for (int index = 0; index < expected; index++) values[index] = Convert.ToDouble(array.GetValue(start + index));
            return values;
        }

        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        private sealed class PrimitiveReader : COM.InwSimplePrimitivesCB
        {
            private readonly FaceAccumulator faces;
            private readonly double[] matrix;
            private readonly double scale;
            public PrimitiveReader(FaceAccumulator faces, double[] matrix, double scale)
            { this.faces = faces; this.matrix = matrix; this.scale = scale; }
            private void Add(COM.InwSimpleVertex vertex)
            {
                var p = ReadArray(vertex.coord, 3);
                double w = p[0] * matrix[3] + p[1] * matrix[7] + p[2] * matrix[11] + matrix[15];
                if (w == 0) throw new InvalidOperationException("Invalid native fragment transform.");
                faces.Add(new RoutePoint(
                    (p[0] * matrix[0] + p[1] * matrix[4] + p[2] * matrix[8] + matrix[12]) / w * scale,
                    (p[0] * matrix[1] + p[1] * matrix[5] + p[2] * matrix[9] + matrix[13]) / w * scale,
                    (p[0] * matrix[2] + p[1] * matrix[6] + p[2] * matrix[10] + matrix[14]) / w * scale));
            }
            public void Triangle(COM.InwSimpleVertex first, COM.InwSimpleVertex second, COM.InwSimpleVertex third)
            { Add(first); Add(second); Add(third); }
            public void Line(COM.InwSimpleVertex first, COM.InwSimpleVertex second) { Add(first); Add(second); }
            public void Point(COM.InwSimpleVertex vertex) { Add(vertex); }
            public void SnapPoint(COM.InwSimpleVertex vertex) { Add(vertex); }
        }

        private sealed class FaceAccumulator
        {
            private readonly RoutePoint min, max;
            private readonly CancellationToken token;
            private readonly double[][] low = new double[6][], high = new double[6][];
            private readonly int[] count = new int[6];
            private int vertices;
            private bool outsideBounds;
            public FaceAccumulator(RoutePoint min, RoutePoint max, CancellationToken token)
            {
                this.min = min; this.max = max; this.token = token;
                for (int face = 0; face < 6; face++)
                {
                    low[face] = new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };
                    high[face] = new[] { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
                }
            }
            public void Add(RoutePoint point)
            {
                if ((++vertices & 255) == 0) token.ThrowIfCancellationRequested();
                if (point.X < min.X - FaceBandMeters || point.X > max.X + FaceBandMeters
                    || point.Y < min.Y - FaceBandMeters || point.Y > max.Y + FaceBandMeters
                    || point.Z < min.Z - FaceBandMeters || point.Z > max.Z + FaceBandMeters) { outsideBounds = true; return; }
                Accumulate(0, Math.Abs(point.X - min.X), point); Accumulate(1, Math.Abs(point.X - max.X), point);
                Accumulate(2, Math.Abs(point.Y - min.Y), point); Accumulate(3, Math.Abs(point.Y - max.Y), point);
                Accumulate(4, Math.Abs(point.Z - min.Z), point); Accumulate(5, Math.Abs(point.Z - max.Z), point);
            }
            private void Accumulate(int face, double distance, RoutePoint point)
            {
                if (distance > FaceBandMeters) return;
                count[face]++;
                low[face][0] = Math.Min(low[face][0], point.X); high[face][0] = Math.Max(high[face][0], point.X);
                low[face][1] = Math.Min(low[face][1], point.Y); high[face][1] = Math.Max(high[face][1], point.Y);
                low[face][2] = Math.Min(low[face][2], point.Z); high[face][2] = Math.Max(high[face][2], point.Z);
            }
            public IReadOnlyList<TrayFaceExtent> Result()
            {
                if (outsideBounds) return new TrayFaceExtent[0]; // Reject incompatible unit/transform conventions.
                var result = new List<TrayFaceExtent>();
                for (int face = 0; face < 6; face++)
                    if (count[face] > 0) result.Add(new TrayFaceExtent((TrayBoxFace)face,
                        new RoutePoint(low[face][0], low[face][1], low[face][2]),
                        new RoutePoint(high[face][0], high[face][1], high[face][2]), count[face]));
                return result;
            }
        }
    }
}
