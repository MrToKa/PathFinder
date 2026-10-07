using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace AddinRibbon.Routing
{
    /// <summary>
    /// Builds a fresh sparse graph of detached tray polylines. An AABB tree finds
    /// candidate pieces, then cross-tray edges join analytical closest contacts only.
    /// Vertices, endpoint projections and contacts retain the complete polyline length.
    /// </summary>
    public sealed class RouteCalculator
    {
        /// <summary>
        /// A cross-tray gap of at most one micrometre is treated as numerical contact
        /// for route preference. Its actual length still contributes to cable length.
        /// </summary>
        public const double ConnectionGapEpsilonMeters = 0.000001;

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
            if (!RoutePoint.IsFinite(first.Distance) || !RoutePoint.IsFinite(last.Distance))
                return RouteResult.Failure("The endpoint distances are outside the supported coordinate range.");
            var nodes = new List<Node>();
            var pieces = new List<Piece>();
            var segmentNodes = new List<List<int>>();
            int firstNode = -1, lastNode = -1;
            for (int segmentIndex = 0; segmentIndex < eligible.Count; segmentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tray = eligible[segmentIndex];
                int pieceOffset = pieces.Count;
                var vertices = new List<Vertex>();
                var orderedNodes = new List<int>();
                segmentNodes.Add(orderedNodes);
                double along = 0;
                for (int part = 0; part < tray.Points.Count - 1; part++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var start = tray.Points[part];
                    var end = tray.Points[part + 1];
                    double length = start.DistanceTo(end);
                    if (!RoutePoint.IsFinite(length))
                        return RouteResult.Failure("The selected tray geometry contains a length outside the supported coordinate range.");
                    if (length > 0) pieces.Add(new Piece(segmentIndex, start, end, along, length,
                        tray.ConnectionsAtEndsOnly, pieces.Count == pieceOffset));
                    vertices.Add(new Vertex(along, start));
                    along += length;
                }
                if (pieces.Count == pieceOffset)
                    pieces.Add(new Piece(segmentIndex, tray.Points[0], tray.Points[0], 0, 0,
                        tray.ConnectionsAtEndsOnly, true));
                var lastPiece = pieces[pieces.Count - 1];
                lastPiece.HasEndPort = true;
                pieces[pieces.Count - 1] = lastPiece;
                vertices.Add(new Vertex(along, tray.Points[tray.Points.Count - 1]));
                if (first.Segment == segmentIndex) vertices.Add(new Vertex(first.Along, first.Point));
                if (last.Segment == segmentIndex) vertices.Add(new Vertex(last.Along, last.Point));
                vertices.Sort((left, right) => left.Along.CompareTo(right.Along));
                int previous = -1;
                double previousAlong = double.NaN;
                foreach (var vertex in vertices)
                {
                    // Equal abscissae are the same vertex, even at a zero-length piece.
                    int index;
                    if (previous >= 0 && vertex.Along == previousAlong) index = previous;
                    else
                    {
                        if (nodes.Count >= maxNodes) return NodeLimitFailure(maxNodes);
                        index = nodes.Count;
                        nodes.Add(new Node(vertex.Point, segmentIndex, vertex.Along));
                        orderedNodes.Add(index);
                        previous = index;
                        previousAlong = vertex.Along;
                    }
                    if (first.Segment == segmentIndex && vertex.Along == first.Along) firstNode = index;
                    if (last.Segment == segmentIndex && vertex.Along == last.Along) lastNode = index;
                }
            }

            GraphLimit limit;
            if (!AddNeighbourConnections(nodes, pieces, segmentNodes, tolerance, maxNodes,
                maxConnections, cancellationToken, out limit))
                return limit == GraphLimit.Connections ? ConnectionLimitFailure(maxConnections) :
                    limit == GraphLimit.Candidates ? CandidateLimitFailure(maxConnections) : NodeLimitFailure(maxNodes);
            PathCost graphCost;
            var path = ShortestPath(nodes, firstNode, lastNode, cancellationToken, out graphCost);
            if (path == null)
                return RouteResult.Failure("Nearest From tray " + eligible[first.Segment].RouteCode +
                    " (" + first.Distance.ToString("0.###", CultureInfo.InvariantCulture) + " m from object) and To tray " +
                    eligible[last.Segment].RouteCode + " (" + last.Distance.ToString("0.###", CultureInfo.InvariantCulture) +
                    " m from object) are disconnected at " + tolerance.ToString("0.###", CultureInfo.InvariantCulture) +
                    " m connection tolerance. Check route assignments and geometry between these trays.");

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
            double totalLength = graphCost.PhysicalLength + first.Distance + last.Distance;
            if (!RoutePoint.IsFinite(totalLength))
                return RouteResult.Failure("The total cable length is outside the supported coordinate range.");
            return new RouteResult(true, "Path calculated.", codes, segmentIds, points,
                totalLength, first.Distance, last.Distance,
                fromSecondary, toSecondary, graphCost.GapCount, graphCost.GapLength);
        }

        private static RouteResult NodeLimitFailure(int maxNodes)
        {
            return RouteResult.Failure("The selected tray geometry exceeds the " + maxNodes +
                " routing-node limit. Select a smaller tray area or simplify the tray geometry.");
        }

        private static RouteResult ConnectionLimitFailure(int maxConnections)
        {
            return RouteResult.Failure("The selected tray geometry exceeds the " + maxConnections +
                " routing-connection limit. Select a smaller tray area or use a tighter connection tolerance.");
        }

        private static RouteResult CandidateLimitFailure(int maxConnections)
        {
            return RouteResult.Failure("The selected tray geometry exceeds the " + CandidateLimit(maxConnections) +
                " routing-candidate limit. Too many overlapping piece bounds need comparison. Select a smaller tray area.");
        }

        private static long CandidateLimit(int maxConnections) { return 8L * maxConnections; }

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

        private static void Connect(List<Node> nodes, int first, int last, double length, bool isCrossTray = false)
        {
            int gapCount = isCrossTray && length > ConnectionGapEpsilonMeters ? 1 : 0;
            nodes[first].Edges.Add(new Edge(last, length, gapCount));
            nodes[last].Edges.Add(new Edge(first, length, gapCount));
        }

        private static bool AddNeighbourConnections(List<Node> nodes, List<Piece> pieces,
            List<List<int>> segmentNodes, double tolerance, int maxNodes, int maxConnections,
            CancellationToken token, out GraphLimit limit)
        {
            var graph = new ContactGraph(nodes, segmentNodes, maxNodes, maxConnections);
            if (graph.Limit != GraphLimit.None) { limit = graph.Limit; return false; }
            var indices = Enumerable.Range(0, pieces.Count).ToArray();
            var tree = BuildAabbTree(pieces, indices, 0, indices.Length, token);
            long candidates = 0;
            int treeVisits = 0;
            for (int index = 0; index < pieces.Count; index++)
            {
                if ((index & 127) == 0) token.ThrowIfCancellationRequested();
                if (!VisitCandidates(tree, indices, pieces, index, tolerance, graph,
                    token, ref candidates, CandidateLimit(maxConnections), ref treeVisits))
                {
                    limit = graph.Limit == GraphLimit.None ? GraphLimit.Candidates : graph.Limit;
                    return false;
                }
            }
            graph.BuildEdges(token);
            limit = GraphLimit.None;
            return true;
        }

        private static AabbNode BuildAabbTree(List<Piece> pieces, int[] indices, int start,
            int count, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var bounds = pieces[indices[start]].Bounds;
            var center = bounds.CenterPoint;
            var centers = new Bounds(center, center);
            for (int offset = 1; offset < count; offset++)
            {
                if ((offset & 255) == 0) token.ThrowIfCancellationRequested();
                var pieceBounds = pieces[indices[start + offset]].Bounds;
                bounds = Bounds.Union(bounds, pieceBounds);
                center = pieceBounds.CenterPoint;
                centers = Bounds.Union(centers, new Bounds(center, center));
            }
            var node = new AabbNode(bounds, start, count);
            if (count <= 8) return node;
            // Split by the spread of centres, not long piece extents: parallel
            // 100 m pieces can differ only in Y even when their X extent dominates.
            int axis = centers.LongestAxis;
            Array.Sort(indices, start, count, new PieceComparer(pieces, axis));
            token.ThrowIfCancellationRequested();
            int leftCount = count / 2;
            node.Left = BuildAabbTree(pieces, indices, start, leftCount, token);
            node.Right = BuildAabbTree(pieces, indices, start + leftCount, count - leftCount, token);
            return node;
        }

        private static bool VisitCandidates(AabbNode node, int[] indices, List<Piece> pieces,
            int firstIndex, double tolerance, ContactGraph graph, CancellationToken token,
            ref long candidates, long maxCandidates, ref int treeVisits)
        {
            if ((++treeVisits & 255) == 0) token.ThrowIfCancellationRequested();
            var first = pieces[firstIndex];
            if (!Bounds.WithinTolerance(first.Bounds, node.Bounds, tolerance)) return true;
            if (node.Left != null)
                return VisitCandidates(node.Left, indices, pieces, firstIndex, tolerance,
                    graph, token, ref candidates, maxCandidates, ref treeVisits) &&
                    VisitCandidates(node.Right, indices, pieces, firstIndex, tolerance,
                    graph, token, ref candidates, maxCandidates, ref treeVisits);
            for (int offset = 0; offset < node.Count; offset++)
            {
                int lastIndex = indices[node.Start + offset];
                if (lastIndex <= firstIndex) continue;
                var last = pieces[lastIndex];
                if (last.Segment == first.Segment ||
                    !Bounds.WithinTolerance(first.Bounds, last.Bounds, tolerance)) continue;
                if (++candidates > maxCandidates) return false;
                if ((candidates & 255) == 0) token.ThrowIfCancellationRequested();
                if (first.EndsOnly || last.EndsOnly)
                {
                    if (!AddPortContacts(first, last, graph, tolerance)) return false;
                    continue;
                }
                double firstFraction, lastFraction, highFirst, highLast;
                bool overlap = ParallelContactRange(first, last,
                    out firstFraction, out lastFraction, out highFirst, out highLast);
                if (!overlap) ClosestPoints(first, last, out firstFraction, out lastFraction);
                if (!graph.AddContact(first, last, firstFraction, lastFraction, tolerance)) return false;
                // Parallel overlap has infinitely many closest contacts. Its two
                // boundaries preserve travel without forcing a detour to one end.
                if (overlap && highFirst != firstFraction &&
                    !graph.AddContact(first, last, highFirst, highLast, tolerance)) return false;
            }
            return true;
        }

        private static bool AddPortContacts(Piece first, Piece last, ContactGraph graph, double tolerance)
        {
            if (first.EndsOnly)
            {
                if (first.HasStartPort && !AddPortContact(first, last, 0, graph, tolerance)) return false;
                if (first.HasEndPort && !AddPortContact(first, last, 1, graph, tolerance)) return false;
                return true;
            }
            if (last.HasStartPort && !graph.AddContact(first, last,
                ProjectionFraction(first, last.Start), 0, tolerance)) return false;
            if (last.HasEndPort && !graph.AddContact(first, last,
                ProjectionFraction(first, last.End), 1, tolerance)) return false;
            return true;
        }

        private static bool AddPortContact(Piece first, Piece last, double firstFraction,
            ContactGraph graph, double tolerance)
        {
            if (last.EndsOnly)
            {
                if (last.HasStartPort && !graph.AddContact(first, last, firstFraction, 0, tolerance)) return false;
                if (last.HasEndPort && !graph.AddContact(first, last, firstFraction, 1, tolerance)) return false;
                return true;
            }
            var point = firstFraction == 0 ? first.Start : first.End;
            return graph.AddContact(first, last, firstFraction, ProjectionFraction(last, point), tolerance);
        }

        private static double ProjectionFraction(Piece piece, RoutePoint target)
        {
            double x = piece.End.X - piece.Start.X, y = piece.End.Y - piece.Start.Y, z = piece.End.Z - piece.Start.Z;
            double lengthSquared = x * x + y * y + z * z;
            return lengthSquared == 0 ? 0 : Clamp(((target.X - piece.Start.X) * x +
                (target.Y - piece.Start.Y) * y + (target.Z - piece.Start.Z) * z) / lengthSquared);
        }

        private static bool ParallelContactRange(Piece first, Piece last,
            out double lowFirst, out double lowLast, out double highFirst, out double highLast)
        {
            lowFirst = lowLast = highFirst = highLast = 0;
            double ax = first.End.X - first.Start.X, ay = first.End.Y - first.Start.Y, az = first.End.Z - first.Start.Z;
            double bx = last.End.X - last.Start.X, by = last.End.Y - last.Start.Y, bz = last.End.Z - last.Start.Z;
            double a = ax * ax + ay * ay + az * az;
            double b = bx * bx + by * by + bz * bz;
            if (a == 0 || b == 0) return false;
            double cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
            // Only numerical representations of parallel directions qualify; a
            // shallow real intersection still uses its unique analytical contact.
            if (cx * cx + cy * cy + cz * cz > a * b * 1e-24) return false;
            double rx = last.Start.X - first.Start.X, ry = last.Start.Y - first.Start.Y, rz = last.Start.Z - first.Start.Z;
            double start = (rx * ax + ry * ay + rz * az) / a;
            double delta = (ax * bx + ay * by + az * bz) / a;
            double end = start + delta;
            lowFirst = Math.Max(0, Math.Min(start, end));
            highFirst = Math.Min(1, Math.Max(start, end));
            if (lowFirst > highFirst) return false;
            lowLast = Clamp((lowFirst - start) / delta);
            highLast = Clamp((highFirst - start) / delta);
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
            CancellationToken token, out PathCost cost)
        {
            var distance = Enumerable.Repeat(PathCost.Unreachable, nodes.Count).ToArray();
            var previous = Enumerable.Repeat(-1, nodes.Count).ToArray();
            var settled = new bool[nodes.Count];
            var queue = new MinHeap();
            distance[first] = new PathCost(0, 0, 0);
            queue.Add(new QueueEntry(first, distance[first]));
            int visits = 0;
            int relaxations = 0;
            while (queue.Count > 0)
            {
                if ((++visits & 127) == 0) token.ThrowIfCancellationRequested();
                var entry = queue.RemoveFirst();
                if (settled[entry.Node] || entry.Cost.CompareTo(distance[entry.Node]) > 0) continue;
                settled[entry.Node] = true;
                if (entry.Node == last) break;
                foreach (var edge in nodes[entry.Node].Edges)
                {
                    if ((++relaxations & 255) == 0) token.ThrowIfCancellationRequested();
                    if (settled[edge.Node]) continue;
                    var candidate = entry.Cost.Add(edge);
                    if (!RoutePoint.IsFinite(candidate.GapLength) || !RoutePoint.IsFinite(candidate.PhysicalLength)) continue;
                    // Strict improvements only: equal-cost zero-length contacts
                    // must never rewrite predecessors into a cycle.
                    if (candidate.CompareTo(distance[edge.Node]) < 0)
                    {
                        distance[edge.Node] = candidate;
                        previous[edge.Node] = entry.Node;
                        queue.Add(new QueueEntry(edge.Node, candidate));
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            cost = distance[last];
            if (cost.GapCount == int.MaxValue) return null;
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
            internal Node(RoutePoint point, int segment, double along) { Point = point; Segment = segment; Along = along; }
        }

        private struct Edge
        {
            internal readonly int Node;
            internal readonly int GapCount;
            internal readonly double Length;
            internal Edge(int node, double length, int gapCount) { Node = node; Length = length; GapCount = gapCount; }
        }

        private struct Vertex
        {
            internal readonly double Along;
            internal readonly RoutePoint Point;
            internal Vertex(double along, RoutePoint point) { Along = along; Point = point; }
        }

        private struct Piece
        {
            internal readonly int Segment;
            internal readonly RoutePoint Start, End;
            internal readonly double Along, Length;
            internal readonly Bounds Bounds;
            internal readonly bool EndsOnly, HasStartPort;
            internal bool HasEndPort;
            internal Piece(int segment, RoutePoint start, RoutePoint end, double along, double length,
                bool endsOnly, bool hasStartPort)
            {
                Segment = segment; Start = start; End = end; Along = along; Length = length;
                Bounds = new Bounds(start, end);
                EndsOnly = endsOnly; HasStartPort = hasStartPort; HasEndPort = false;
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

        private enum GraphLimit { None, Nodes, Connections, Candidates }

        private sealed class ContactGraph
        {
            private readonly List<Node> nodes;
            private readonly List<List<int>> segmentNodes;
            private readonly int maxNodes, maxConnections;
            private readonly List<Connection> crossConnections = new List<Connection>();
            private readonly HashSet<long> connectedNodes = new HashSet<long>();
            private int connections;
            internal GraphLimit Limit { get; private set; }

            internal ContactGraph(List<Node> nodes, List<List<int>> segmentNodes, int maxNodes, int maxConnections)
            {
                this.nodes = nodes; this.segmentNodes = segmentNodes;
                this.maxNodes = maxNodes; this.maxConnections = maxConnections;
                foreach (var ordered in segmentNodes) connections += ordered.Count - 1;
                if (connections > maxConnections) Limit = GraphLimit.Connections;
            }

            internal bool AddContact(Piece first, Piece last, double firstFraction, double lastFraction, double tolerance)
            {
                var firstPoint = RoutePoint.Interpolate(first.Start, first.End, firstFraction);
                var lastPoint = RoutePoint.Interpolate(last.Start, last.End, lastFraction);
                double distance = firstPoint.DistanceTo(lastPoint);
                if (distance > tolerance) return true;
                int firstNode = Insert(first, firstPoint, firstFraction);
                if (firstNode < 0) return false;
                int lastNode = Insert(last, lastPoint, lastFraction);
                if (lastNode < 0) return false;
                long key = ((long)Math.Min(firstNode, lastNode) << 32) | (uint)Math.Max(firstNode, lastNode);
                if (!connectedNodes.Add(key)) return true;
                if (connections >= maxConnections) { Limit = GraphLimit.Connections; return false; }
                crossConnections.Add(new Connection(firstNode, lastNode, distance));
                connections++;
                return true;
            }

            private int Insert(Piece piece, RoutePoint point, double fraction)
            {
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
                if (nodes.Count >= maxNodes) { Limit = GraphLimit.Nodes; return -1; }
                // Splitting one continuous polyline edge introduces one new edge.
                if (connections >= maxConnections) { Limit = GraphLimit.Connections; return -1; }
                int index = nodes.Count;
                nodes.Add(new Node(point, piece.Segment, along));
                ordered.Insert(low, index);
                connections++;
                return index;
            }

            internal void BuildEdges(CancellationToken token)
            {
                int built = 0;
                foreach (var ordered in segmentNodes)
                    for (int offset = 1; offset < ordered.Count; offset++)
                    {
                        if ((++built & 255) == 0) token.ThrowIfCancellationRequested();
                        int first = ordered[offset - 1], last = ordered[offset];
                        Connect(nodes, first, last, nodes[last].Along - nodes[first].Along);
                    }
                foreach (var connection in crossConnections)
                {
                    if ((++built & 255) == 0) token.ThrowIfCancellationRequested();
                    Connect(nodes, connection.First, connection.Last, connection.Length, isCrossTray: true);
                }
                token.ThrowIfCancellationRequested();
            }
        }

        private struct Connection
        {
            internal readonly int First, Last;
            internal readonly double Length;
            internal Connection(int first, int last, double length) { First = first; Last = last; Length = length; }
        }

        private struct Bounds
        {
            internal readonly double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
            internal Bounds(RoutePoint first, RoutePoint last)
            {
                MinX = Math.Min(first.X, last.X); MinY = Math.Min(first.Y, last.Y); MinZ = Math.Min(first.Z, last.Z);
                MaxX = Math.Max(first.X, last.X); MaxY = Math.Max(first.Y, last.Y); MaxZ = Math.Max(first.Z, last.Z);
            }
            private Bounds(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
            { MinX = minX; MinY = minY; MinZ = minZ; MaxX = maxX; MaxY = maxY; MaxZ = maxZ; }
            internal static Bounds Union(Bounds first, Bounds last)
            {
                return new Bounds(Math.Min(first.MinX, last.MinX), Math.Min(first.MinY, last.MinY), Math.Min(first.MinZ, last.MinZ),
                    Math.Max(first.MaxX, last.MaxX), Math.Max(first.MaxY, last.MaxY), Math.Max(first.MaxZ, last.MaxZ));
            }
            internal int LongestAxis
            {
                get
                {
                    double x = MaxX - MinX, y = MaxY - MinY, z = MaxZ - MinZ;
                    return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
                }
            }
            internal double Center(int axis)
            {
                return axis == 0 ? MinX + (MaxX - MinX) / 2 :
                    axis == 1 ? MinY + (MaxY - MinY) / 2 : MinZ + (MaxZ - MinZ) / 2;
            }
            internal RoutePoint CenterPoint { get { return new RoutePoint(Center(0), Center(1), Center(2)); } }
            internal static bool WithinTolerance(Bounds first, Bounds last, double tolerance)
            {
                double x = Math.Max(0, Math.Max(first.MinX - last.MaxX, last.MinX - first.MaxX));
                double y = Math.Max(0, Math.Max(first.MinY - last.MaxY, last.MinY - first.MaxY));
                double z = Math.Max(0, Math.Max(first.MinZ - last.MaxZ, last.MinZ - first.MaxZ));
                return x <= tolerance && y <= tolerance && z <= tolerance &&
                    x * x + y * y + z * z <= tolerance * tolerance;
            }
        }

        private sealed class AabbNode
        {
            internal readonly Bounds Bounds;
            internal readonly int Start, Count;
            internal AabbNode Left, Right;
            internal AabbNode(Bounds bounds, int start, int count) { Bounds = bounds; Start = start; Count = count; }
        }

        private sealed class PieceComparer : IComparer<int>
        {
            private readonly List<Piece> pieces;
            private readonly int axis;
            internal PieceComparer(List<Piece> pieces, int axis) { this.pieces = pieces; this.axis = axis; }
            public int Compare(int first, int last)
            {
                int compared = pieces[first].Bounds.Center(axis).CompareTo(pieces[last].Bounds.Center(axis));
                return compared != 0 ? compared : first.CompareTo(last);
            }
        }

        // All edge components are non-negative. Their additive lexicographic
        // order makes Dijkstra valid without an arbitrary scalar gap penalty.
        private struct PathCost : IComparable<PathCost>
        {
            internal static readonly PathCost Unreachable = new PathCost(int.MaxValue, double.PositiveInfinity, double.PositiveInfinity);
            internal readonly int GapCount;
            internal readonly double GapLength, PhysicalLength;
            internal PathCost(int gapCount, double gapLength, double physicalLength)
            { GapCount = gapCount; GapLength = gapLength; PhysicalLength = physicalLength; }
            internal PathCost Add(Edge edge)
            {
                return new PathCost(GapCount + edge.GapCount,
                    GapLength + (edge.GapCount == 0 ? 0 : edge.Length), PhysicalLength + edge.Length);
            }
            public int CompareTo(PathCost other)
            {
                int count = GapCount.CompareTo(other.GapCount);
                if (count != 0) return count;
                int gaps = GapLength.CompareTo(other.GapLength);
                return gaps != 0 ? gaps : PhysicalLength.CompareTo(other.PhysicalLength);
            }
        }

        private struct QueueEntry
        {
            internal readonly int Node;
            internal readonly PathCost Cost;
            internal QueueEntry(int node, PathCost cost) { Node = node; Cost = cost; }
        }

        // Kept local for .NET Framework compatibility; no host-side queue or graph state is reused.
        private sealed class MinHeap
        {
            private readonly List<QueueEntry> entries = new List<QueueEntry>();
            internal int Count { get { return entries.Count; } }
            private static bool Before(QueueEntry first, QueueEntry second)
            {
                int order = first.Cost.CompareTo(second.Cost);
                return order < 0 || (order == 0 && first.Node < second.Node);
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
