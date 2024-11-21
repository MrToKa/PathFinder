namespace AddinRibbon.Dijkstra
{
    public class Edge
    {
        public Node TargetNode { get; }
        public double Weight { get; }

        public Edge(Node targetNode, double weight)
        {
            TargetNode = targetNode;
            Weight = weight;
        }
    }
}