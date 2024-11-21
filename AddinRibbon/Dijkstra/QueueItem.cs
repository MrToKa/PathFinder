using System;

namespace AddinRibbon.Dijkstra
{
    public class QueueItem : IComparable<QueueItem>
    {
        public double Distance { get; set; }
        public Node Node { get; set; }

        public int CompareTo(QueueItem other)
        {
            int compare = Distance.CompareTo(other.Distance);
            if (compare == 0)
            {
                // Compare nodes uniquely to avoid treating different nodes with the same distance as equal
                compare = Node.Id.CompareTo(other.Node.Id);
            }
            return compare;
        }

        public override bool Equals(object obj)
        {
            if (obj is QueueItem other)
                return Node.Equals(other.Node);
            return false;
        }

        public override int GetHashCode()
        {
            return Node.GetHashCode();
        }

    }
}
