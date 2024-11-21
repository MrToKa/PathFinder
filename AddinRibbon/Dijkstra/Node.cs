using System.Collections.Generic;

namespace AddinRibbon.Dijkstra
{
    public class Node
    {
        private static int _idCounter = 0;

        public int Id { get; private set; }
        public string TrayName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public List<Edge> Edges { get; set; } = new List<Edge>();

        public Node()
        {
            Id = _idCounter++;
        }

        public override bool Equals(object obj)
        {
            if (obj is Node other)
            {
                return Id == other.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id;
        }
    }
}