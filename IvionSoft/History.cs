using System;
using System.Collections.Generic;

namespace IvionSoft
{
    public class History<T>
    {
        int length;
        HashSet<T> hashes;
        Queue<T> queue;


        public History(int length)
        {
            if (length < 1)
                throw new ArgumentException("Must be 1 or larger", "length");

            this.length = length;
            hashes = new HashSet<T>();
            queue = new Queue<T>(length + 1);
        }

        public bool Contains(T item)
        {
            return hashes.Contains(item);
        }

        public bool Add(T item)
        {
            bool added = hashes.Add(item);

            if (added)
            {
                queue.Enqueue(item);
                if (queue.Count > length)
                {
                    T toRemove = queue.Dequeue();
                    hashes.Remove(toRemove);
                }
            }

            return added;
        }
    }
}