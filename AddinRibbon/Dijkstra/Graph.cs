using System.Collections.Generic;
using System;

namespace AddinRibbon.Dijkstra
{
    public class Graph
    {
        public List<Node> Nodes { get; set; } = new List<Node>();

        public void AddNode(Node node)
        {
            Nodes.Add(node);
        }

        public void AddBidirectionalEdge(Node fromNode, Node toNode)
        {
            double weight = CalculateDistance(fromNode, toNode);
            fromNode.Edges.Add(new Edge { ToNode = toNode, Weight = weight });
            toNode.Edges.Add(new Edge { ToNode = fromNode, Weight = weight });
        }

        private double CalculateDistance(Node a, Node b)
        {
            return Math.Sqrt(
                Math.Pow(a.X - b.X, 2) +
                Math.Pow(a.Y - b.Y, 2) +
                Math.Pow(a.Z - b.Z, 2));
        }
    }
}
