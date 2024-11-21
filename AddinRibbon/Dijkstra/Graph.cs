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

        public void AddBidirectionalEdge(Node nodeA, Node nodeB, double weight)
        {
            nodeA.Edges.Add(new Edge(nodeB, weight));
            nodeB.Edges.Add(new Edge(nodeA, weight));
        }
    }
}
