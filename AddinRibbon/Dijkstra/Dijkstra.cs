using System.Collections.Generic;

namespace AddinRibbon.Dijkstra
{
    public class Dijkstra
    {
        public static (List<Node> path, double totalDistance) FindShortestPath(Graph graph, Node start, Node end)
        {
            var distances = new Dictionary<Node, double>();
            var previous = new Dictionary<Node, Node>();
            var priorityQueue = new PriorityQueue<Node>();
            var visited = new HashSet<Node>();

            distances[start] = 0;
            priorityQueue.Enqueue(start, 0);

            while (!priorityQueue.IsEmpty())
            {
                var currentNode = priorityQueue.Dequeue();

                if (visited.Contains(currentNode))
                    continue;

                visited.Add(currentNode);

                if (currentNode.Equals(end))
                {
                    // Reconstruct the path
                    var path = new List<Node>();
                    var node = end;
                    while (node != null)
                    {
                        path.Add(node);
                        previous.TryGetValue(node, out node);
                    }
                    path.Reverse();
                    return (path, distances[end]);
                }

                foreach (var edge in currentNode.Edges)
                {
                    var neighbor = edge.ToNode;
                    var newDist = distances[currentNode] + edge.Weight;

                    if (!distances.ContainsKey(neighbor) || newDist < distances[neighbor])
                    {
                        distances[neighbor] = newDist;
                        previous[neighbor] = currentNode;
                        priorityQueue.Enqueue(neighbor, newDist);
                    }
                }
            }

            return (null, double.PositiveInfinity);
        }


    }
}
