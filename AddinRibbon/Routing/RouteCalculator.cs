using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AddinRibbon.Routing
{
    /// <summary>
    /// Builds a fresh graph of detached tray polylines. Neighbour connections never
    /// exceed the supplied tolerance; disconnected networks remain disconnected.
    /// </summary>
    public sealed class RouteCalculator
    {
        public RouteResult Calculate(IEnumerable<TraySegment> trays, RoutePoint from, RoutePoint to,
            CableCategory category, RoutingOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trays == null) throw new ArgumentNullException("trays");
            if (category != CableCategory.MV && category != CableCategory.LV && category != CableCategory.Control)
                throw new ArgumentException("Select one cable category.", "category");
            options = options ?? new RoutingOptions();
            options.Validate();
            // Snapshot mutable UI options before doing background work.
            double tolerance = options.ConnectionToleranceMeters;
            double spacing = Math.Min(options.SampleSpacingMeters, tolerance / 2);
            double secondaryDistance = options.SecondaryDistanceMeters;
            int maxNodes = options.MaxGraphNodes;
            int maxConnections = options.MaxGraphConnections;
            var eligible = new List<TraySegment>();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tray in trays)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tray == null) throw new ArgumentException("Tray snapshots cannot contain null entries.", "trays");
                if ((tray.AllowedCategories & category) != category) continue;
                if (!identifiers.Add(tray.Id))
                    throw new ArgumentException("Duplicate tray identifier: " + tray.Id, "trays");
                eligible.Add(tray);
            }
            eligible.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            if (eligible.Count == 0) return RouteResult.Failure("No selected trays allow this cable category.");

            Projection first = FindNearest(eligible, from, cancellationToken);
            Projection last = FindNearest(eligible, to, cancellationToken);
            var nodes = new List<Node>();
            var pieces = new List<Piece>();
            var segmentNodes = new List<List<int>>();
            int firstNode = -1, lastNode = -1;
            for (int segmentIndex = 0; segmentIndex < eligible.Count; segmentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tray = eligible[segmentIndex];
                int pieceOffset = pieces.Count;
                var samples = new List<Sample>();
                var orderedNodes = new List<int>();
                segmentNodes.Add(orderedNodes);
                double along = 0;
                for (int part = 0; part < tray.Points.Count - 1; part++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var start = tray.Points[part];
                    var end = tray.Points[part + 1];
                    double length = start.DistanceTo(end);
                    int pieceIndex = pieces.Count;
                    pieces.Add(new Piece(segmentIndex, start, end, along, length));
                    samples.Add(new Sample(along, start, pieceIndex));
                    double required = Math.Ceiling(length / spacing);
                    if (!RoutePoint.IsFinite(required) || required > maxNodes - nodes.Count - samples.Count)
                        return NodeLimitFailure(maxNodes);
                    int subdivisions = Math.Max(1, (int)required);
                    for (int sample = 1; sample <= subdivisions; sample++)
                    {
                        if ((sample & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                        double fraction = (double)sample / subdivisions;
                        samples.Add(new Sample(along + length * fraction,
                            RoutePoint.Interpolate(start, end, fraction), pieceIndex));
                    }
                    along += length;
                }
                if (first.Segment == segmentIndex) samples.Add(new Sample(first.Along, first.Point, pieceOffset + first.Part));
                if (last.Segment == segmentIndex) samples.Add(new Sample(last.Along, last.Point, pieceOffset + last.Part));
                samples.Sort((left, right) => left.Along.CompareTo(right.Along));
                int previous = -1;
                double previousAlong = double.NaN;
                foreach (var sample in samples)
                {
                    // Equal abscissae are the same vertex, even at a zero-length piece.
                    int index;
                    if (previous >= 0 && sample.Along == previousAlong) index = previous;
                    else
                    {
                        if (nodes.Count >= maxNodes) return NodeLimitFailure(maxNodes);
                        index = nodes.Count;
                        nodes.Add(new Node(sample.Point, segmentIndex, sample.Along));
                        orderedNodes.Add(index);
                        if (previous >= 0) Connect(nodes, previous, index,
                            nodes[previous].Point.DistanceTo(sample.Point));
                        previous = index;
                        previousAlong = sample.Along;
                    }
                    if (!nodes[index].Pieces.Contains(sample.Piece)) nodes[index].Pieces.Add(sample.Piece);
                    if (first.Segment == segmentIndex && sample.Along == first.Along) firstNode = index;
                    if (last.Segment == segmentIndex && sample.Along == last.Along) lastNode = index;
                }
            }

            bool connectionLimitReached;
            if (!AddNeighbourConnections(nodes, pieces, segmentNodes, tolerance, maxNodes,
                maxConnections, cancellationToken, out connectionLimitReached))
                return connectionLimitReached ? ConnectionLimitFailure(maxConnections) : NodeLimitFailure(maxNodes);
            double graphLength;
            var path = ShortestPath(nodes, firstNode, lastNode, cancellationToken, out graphLength);
            if (path == null)
                return RouteResult.Failure("The nearest eligible trays are disconnected at the selected connection tolerance.");

            var codes = new List<string>();
            var segmentIds = new List<string>();
            var points = new List<RoutePoint> { from };
            bool fromSecondary = first.Distance > secondaryDistance;
            bool toSecondary = last.Distance > secondaryDistance;
            if (fromSecondary) codes.Add("/SECONDARY");
            foreach (int nodeIndex in path)
            {
                var node = nodes[nodeIndex];
                var segment = eligible[node.Segment];
                AppendTransition(codes, segment.RouteCode);
                AppendTransition(segmentIds, segment.Id);
                AppendPoint(points, node.Point);
            }
            AppendPoint(points, to);
            if (toSecondary) codes.Add("/SECONDARY");
            return new RouteResult(true, "Path calculated.", codes, segmentIds, points,
                graphLength + first.Distance + last.Distance, first.Distance, last.Distance,
                fromSecondary, toSecondary);
        }

        private static RouteResult NodeLimitFailure(int maxNodes)
        {
            return RouteResult.Failure("The selected tray geometry exceeds the " + maxNodes +
                " routing-node limit. Select a smaller tray area or increase the sample spacing and connection tolerance.");
        }

        private static RouteResult ConnectionLimitFailure(int maxConnections)
        {
            return RouteResult.Failure("The selected tray geometry exceeds the " + maxConnections +
                " routing-connection limit. Select a smaller tray area or use a tighter connection tolerance.");
        }

        private static void AppendTransition(List<string> values, string value)
        {
            if (values.Count == 0 || !StringComparer.Ordinal.Equals(values[values.Count - 1], value)) values.Add(value);
        }

        private static void AppendPoint(List<RoutePoint> values, RoutePoint value)
        {
            if (values.Count == 0 || values[values.Count - 1].DistanceTo(value) > 0) values.Add(value);
        }

        private static Projection FindNearest(List<TraySegment> trays, RoutePoint target, CancellationToken token)
        {
            var best = new Projection { Distance = double.PositiveInfinity };
            for (int trayIndex = 0; trayIndex < trays.Count; trayIndex++)
            {
                token.ThrowIfCancellationRequested();
                double along = 0;
                var points = trays[trayIndex].Points;
                for (int part = 0; part < points.Count - 1; part++)
                {
                    if ((part & 255) == 0) token.ThrowIfCancellationRequested();
                    var a = points[part];
                    var b = points[part + 1];
                    double x = b.X - a.X, y = b.Y - a.Y, z = b.Z - a.Z;
                    double squaredLength = x * x + y * y + z * z;
                    double length = Math.Sqrt(squaredLength);
                    double fraction = squaredLength == 0 ? 0 : Math.Max(0, Math.Min(1,
                        ((target.X - a.X) * x + (target.Y - a.Y) * y + (target.Z - a.Z) * z) / squaredLength));
                    var projected = RoutePoint.Interpolate(a, b, fraction);
                    double distance = projected.DistanceTo(target);
                    if (distance < best.Distance)
                        best = new Projection { Segment = trayIndex, Part = part, Along = along + length * fraction,
                            Point = projected, Distance = distance };
                    along += length;
                }
            }
            return best;
        }

        private static void Connect(List<Node> nodes, int first, int last, double length)
        {
            nodes[first].Edges.Add(new Edge(last, length));
            nodes[last].Edges.Add(new Edge(first, length));
        }

        private static bool AddNeighbourConnections(List<Node> nodes, List<Piece> pieces,
            List<List<int>> segmentNodes, double tolerance, int maxNodes, int maxConnections,
            CancellationToken token, out bool connectionLimitReached)
        {
            connectionLimitReached = false;
            int connections = nodes.Sum(node => node.Edges.Count) / 2;
            if (connections > maxConnections) { connectionLimitReached = true; return false; }
            var cells = new Dictionary<Cell, List<int>>();
            var checkedPieces = new HashSet<long>();
            int comparisons = 0;
            int sampleCount = nodes.Count;
            for (int index = 0; index < sampleCount; index++)
            {
                if ((index & 127) == 0) token.ThrowIfCancellationRequested();
                var node = nodes[index];
                var cell = Cell.ForPoint(node.Point, tolerance);
                // Half-tolerance sample spacing means a real contact always has
                // samples within two hash cells, even at the tolerance boundary.
                for (long dx = -2; dx <= 2; dx++)
                for (long dy = -2; dy <= 2; dy++)
                for (long dz = -2; dz <= 2; dz++)
                {
                    List<int> nearby;
                    if (!cells.TryGetValue(new Cell(cell.X + dx, cell.Y + dy, cell.Z + dz), out nearby)) continue;
                    foreach (int candidateIndex in nearby)
                    {
                        if ((++comparisons & 255) == 0) token.ThrowIfCancellationRequested();
                        var candidate = nodes[candidateIndex];
                        if (candidate.Segment == node.Segment) continue;
                        double distance = node.Point.DistanceTo(candidate.Point);
                        if (distance <= tolerance &&
                            !ConnectWithLimit(nodes, candidateIndex, index, distance, ref connections, maxConnections))
                        { connectionLimitReached = true; return false; }
                        foreach (int firstPiece in node.Pieces)
                        foreach (int lastPiece in candidate.Pieces)
                        {
                            long pair = ((long)Math.Min(firstPiece, lastPiece) << 32) | (uint)Math.Max(firstPiece, lastPiece);
                            if (!checkedPieces.Add(pair)) continue;
                            double firstFraction, lastFraction;
                            ClosestPoints(pieces[firstPiece], pieces[lastPiece], out firstFraction, out lastFraction);
                            var firstPoint = RoutePoint.Interpolate(pieces[firstPiece].Start, pieces[firstPiece].End, firstFraction);
                            var lastPoint = RoutePoint.Interpolate(pieces[lastPiece].Start, pieces[lastPiece].End, lastFraction);
                            double contactDistance = firstPoint.DistanceTo(lastPoint);
                            if (contactDistance > tolerance) continue;
                            int contactFirst = InsertContact(nodes, segmentNodes, pieces[firstPiece], firstPoint,
                                firstFraction, maxNodes, ref connections, maxConnections, out connectionLimitReached);
                            if (contactFirst < 0) return false;
                            int contactLast = InsertContact(nodes, segmentNodes, pieces[lastPiece], lastPoint,
                                lastFraction, maxNodes, ref connections, maxConnections, out connectionLimitReached);
                            if (contactFirst < 0 || contactLast < 0) return false;
                            if (!ConnectWithLimit(nodes, contactFirst, contactLast, contactDistance, ref connections, maxConnections))
                            { connectionLimitReached = true; return false; }
                        }
                    }
                }
                List<int> bucket;
                if (!cells.TryGetValue(cell, out bucket))
                {
                    bucket = new List<int>();
                    cells.Add(cell, bucket);
                }
                bucket.Add(index);
            }
            return true;
        }

        private static int InsertContact(List<Node> nodes, List<List<int>> segmentNodes,
            Piece piece, RoutePoint point, double fraction, int maxNodes, ref int connections,
            int maxConnections, out bool connectionLimitReached)
        {
            connectionLimitReached = false;
            double along = piece.Along + piece.Length * fraction;
            var ordered = segmentNodes[piece.Segment];
            int low = 0, high = ordered.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (nodes[ordered[middle]].Along < along) low = middle + 1;
                else high = middle;
            }
            if (low < ordered.Count && nodes[ordered[low]].Along == along) return ordered[low];
            if (nodes.Count >= maxNodes) return -1;
            int index = nodes.Count;
            nodes.Add(new Node(point, piece.Segment, along));
            if (low > 0 && !ConnectWithLimit(nodes, ordered[low - 1], index,
                nodes[ordered[low - 1]].Point.DistanceTo(point), ref connections, maxConnections))
            { connectionLimitReached = true; return -1; }
            if (low < ordered.Count && !ConnectWithLimit(nodes, index, ordered[low],
                point.DistanceTo(nodes[ordered[low]].Point), ref connections, maxConnections))
            { connectionLimitReached = true; return -1; }
            ordered.Insert(low, index);
            return index;
        }

        private static bool ConnectWithLimit(List<Node> nodes, int first, int last, double distance,
            ref int connections, int maxConnections)
        {
            if (connections >= maxConnections) return false;
            Connect(nodes, first, last, distance);
            connections++;
            return true;
        }

        private static void ClosestPoints(Piece first, Piece last, out double s, out double t)
        {
            double ax = first.End.X - first.Start.X, ay = first.End.Y - first.Start.Y, az = first.End.Z - first.Start.Z;
            double bx = last.End.X - last.Start.X, by = last.End.Y - last.Start.Y, bz = last.End.Z - last.Start.Z;
            double rx = first.Start.X - last.Start.X, ry = first.Start.Y - last.Start.Y, rz = first.Start.Z - last.Start.Z;
            double a = ax * ax + ay * ay + az * az;
            double e = bx * bx + by * by + bz * bz;
            double f = bx * rx + by * ry + bz * rz;
            if (a == 0 && e == 0) { s = 0; t = 0; return; }
            if (a == 0) { s = 0; t = Clamp(f / e); return; }
            double c = ax * rx + ay * ry + az * rz;
            if (e == 0) { s = Clamp(-c / a); t = 0; return; }
            double b = ax * bx + ay * by + az * bz;
            double denominator = a * e - b * b;
            s = denominator > 0 ? Clamp((b * f - c * e) / denominator) : 0;
            t = (b * s + f) / e;
            if (t < 0) { t = 0; s = Clamp(-c / a); }
            else if (t > 1) { t = 1; s = Clamp((b - c) / a); }
        }

        private static double Clamp(double value)
        {
            return Math.Max(0, Math.Min(1, value));
        }

        private static List<int> ShortestPath(List<Node> nodes, int first, int last,
            CancellationToken token, out double length)
        {
            var distance = Enumerable.Repeat(double.PositiveInfinity, nodes.Count).ToArray();
            var previous = Enumerable.Repeat(-1, nodes.Count).ToArray();
            var settled = new bool[nodes.Count];
            var queue = new MinHeap();
            distance[first] = 0;
            queue.Add(new QueueEntry(first, 0));
            int visits = 0;
            int relaxations = 0;
            while (queue.Count > 0)
            {
                if ((++visits & 127) == 0) token.ThrowIfCancellationRequested();
                var entry = queue.RemoveFirst();
                if (settled[entry.Node] || entry.Distance > distance[entry.Node]) continue;
                settled[entry.Node] = true;
                if (entry.Node == last) break;
                foreach (var edge in nodes[entry.Node].Edges)
                {
                    if ((++relaxations & 255) == 0) token.ThrowIfCancellationRequested();
                    if (settled[edge.Node]) continue;
                    double candidate = entry.Distance + edge.Length;
                    if (candidate < distance[edge.Node])
                    {
                        distance[edge.Node] = candidate;
                        previous[edge.Node] = entry.Node;
                        queue.Add(new QueueEntry(edge.Node, candidate));
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            length = distance[last];
            if (double.IsPositiveInfinity(length)) return null;
            var path = new List<int>();
            for (int node = last; node >= 0; node = previous[node]) path.Add(node);
            path.Reverse();
            return path;
        }

        private sealed class Node
        {
            internal readonly RoutePoint Point;
            internal readonly int Segment;
            internal readonly double Along;
            internal readonly List<Edge> Edges = new List<Edge>();
            internal readonly List<int> Pieces = new List<int>();
            internal Node(RoutePoint point, int segment, double along) { Point = point; Segment = segment; Along = along; }
        }

        private struct Edge
        {
            internal readonly int Node;
            internal readonly double Length;
            internal Edge(int node, double length) { Node = node; Length = length; }
        }

        private struct Sample
        {
            internal readonly double Along;
            internal readonly RoutePoint Point;
            internal readonly int Piece;
            internal Sample(double along, RoutePoint point, int piece) { Along = along; Point = point; Piece = piece; }
        }

        private struct Piece
        {
            internal readonly int Segment;
            internal readonly RoutePoint Start, End;
            internal readonly double Along, Length;
            internal Piece(int segment, RoutePoint start, RoutePoint end, double along, double length)
            {
                Segment = segment; Start = start; End = end; Along = along; Length = length;
            }
        }

        private struct Projection
        {
            internal int Segment;
            internal int Part;
            internal double Along;
            internal RoutePoint Point;
            internal double Distance;
        }

        private struct Cell : IEquatable<Cell>
        {
            internal readonly long X, Y, Z;
            internal Cell(long x, long y, long z) { X = x; Y = y; Z = z; }
            internal static Cell ForPoint(RoutePoint point, double size)
            {
                return new Cell((long)Math.Floor(point.X / size),
                    (long)Math.Floor(point.Y / size), (long)Math.Floor(point.Z / size));
            }
            public bool Equals(Cell other) { return X == other.X && Y == other.Y && Z == other.Z; }
            public override bool Equals(object obj) { return obj is Cell && Equals((Cell)obj); }
            public override int GetHashCode()
            {
                unchecked { return ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode(); }
            }
        }

        private struct QueueEntry
        {
            internal readonly int Node;
            internal readonly double Distance;
            internal QueueEntry(int node, double distance) { Node = node; Distance = distance; }
        }

        // Kept local for .NET Framework compatibility; no host-side queue or graph state is reused.
        private sealed class MinHeap
        {
            private readonly List<QueueEntry> entries = new List<QueueEntry>();
            internal int Count { get { return entries.Count; } }
            private static bool Before(QueueEntry first, QueueEntry second)
            {
                return first.Distance < second.Distance ||
                    (first.Distance == second.Distance && first.Node < second.Node);
            }
            internal void Add(QueueEntry entry)
            {
                entries.Add(entry);
                int child = entries.Count - 1;
                while (child > 0)
                {
                    int parent = (child - 1) / 2;
                    if (!Before(entries[child], entries[parent])) break;
                    var swap = entries[parent]; entries[parent] = entries[child]; entries[child] = swap;
                    child = parent;
                }
            }
            internal QueueEntry RemoveFirst()
            {
                var result = entries[0];
                var tail = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0) return result;
                entries[0] = tail;
                int parent = 0;
                while (true)
                {
                    int child = parent * 2 + 1;
                    if (child >= entries.Count) break;
                    if (child + 1 < entries.Count && Before(entries[child + 1], entries[child])) child++;
                    if (!Before(entries[child], entries[parent])) break;
                    var swap = entries[parent]; entries[parent] = entries[child]; entries[child] = swap;
                    parent = child;
                }
                return result;
            }
        }
    }
}
