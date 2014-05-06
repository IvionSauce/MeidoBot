using System;
using System.Collections.Generic;

namespace IvionSoft
{
    public class History<T>
    {
        public int Length { get; private set; }

        HashSet<T> hashes;
        Queue<T> queue;


        public History() : this(0, null)
        {}

        public History(int length) : this(length, null)
        {}

        public History(int length, IEqualityComparer<T> comparer)
        {
            if (length > 0)
            {
                Length = length;
                queue = new Queue<T>(length + 1);

                if (comparer != null)
                    hashes = new HashSet<T>(comparer);
                else
                    hashes = new HashSet<T>();
            }
            else
                Length = 0;
        }


        public bool Contains(T item)
        {
            if (Length > 0)
                return hashes.Contains(item);
            else
                return false;
        }

        public bool Add(T item)
        {
            if (Length == 0)
                return false;

            bool added;
            added = hashes.Add(item);

            if (added)
            {
                queue.Enqueue(item);
                if (queue.Count > Length)
                {
                    T toRemove = queue.Dequeue();
                    hashes.Remove(toRemove);
                }
            }

            return added;
        }
    }
}