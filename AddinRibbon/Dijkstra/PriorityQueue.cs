using System.Collections.Generic;
using System;

namespace AddinRibbon.Dijkstra
{
    public class PriorityQueue<T>
    {
        private List<(double priority, T item)> elements = new List<(double, T)>();

        public int Count => elements.Count;

        public void Enqueue(T item, double priority)
        {
            elements.Add((priority, item));
            HeapifyUp(elements.Count - 1);
        }

        public T Dequeue()
        {
            if (elements.Count == 0)
                throw new InvalidOperationException("Priority queue is empty.");

            var result = elements[0].item;
            elements[0] = elements[elements.Count - 1];
            elements.RemoveAt(elements.Count - 1);
            HeapifyDown(0);
            return result;
        }

        public bool IsEmpty()
        {
            return elements.Count == 0;
        }

        private void HeapifyUp(int index)
        {
            while (index > 0 && elements[index].priority < elements[(index - 1) / 2].priority)
            {
                var temp = elements[index];
                elements[index] = elements[(index - 1) / 2];
                elements[(index - 1) / 2] = temp;
                index = (index - 1) / 2;
            }
        }

        private void HeapifyDown(int index)
        {
            int smallest = index;
            int leftChild = index * 2 + 1;
            int rightChild = index * 2 + 2;

            if (leftChild < elements.Count && elements[leftChild].priority < elements[smallest].priority)
                smallest = leftChild;

            if (rightChild < elements.Count && elements[rightChild].priority < elements[smallest].priority)
                smallest = rightChild;

            if (smallest != index)
            {
                var temp = elements[index];
                elements[index] = elements[smallest];
                elements[smallest] = temp;
                HeapifyDown(smallest);
            }
        }
    }
}