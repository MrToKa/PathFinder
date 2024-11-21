using System.Collections.Generic;

namespace AddinRibbon.Dijkstra
{
    public class Dijkstra
    {
        public static (List<Node> path, double totalDistance) FindShortestPath(Graph graph, Node startNode, Node endNode)
        {
            var distances = new Dictionary<Node, double>();
            var previousNodes = new Dictionary<Node, Node>();
            var priorityQueue = new PriorityQueue<Node>();

            foreach (var node in graph.Nodes)
            {
                distances[node] = double.PositiveInfinity;
                previousNodes[node] = null;
            }

            distances[startNode] = 0;
            priorityQueue.Enqueue(startNode, 0);

            while (priorityQueue.Count > 0)
            {
                var currentNode = priorityQueue.Dequeue();

                if (currentNode == endNode)
                    break;

                foreach (var edge in currentNode.Edges)
                {
                    var neighbor = edge.TargetNode;
                    double newDist = distances[currentNode] + edge.Weight;

                    if (newDist < distances[neighbor])
                    {
                        distances[neighbor] = newDist;
                        previousNodes[neighbor] = currentNode;
                        priorityQueue.Enqueue(neighbor, newDist);
                    }
                }
            }

            // Reconstruct the path
            var path = new List<Node>();
            var nodeToAdd = endNode;

            if (previousNodes[endNode] == null)
            {
                return (null, double.PositiveInfinity); // No path found
            }

            while (nodeToAdd != null)
            {
                path.Insert(0, nodeToAdd);
                nodeToAdd = previousNodes[nodeToAdd];
            }

            return (path, distances[endNode]);
        }

    }
}
