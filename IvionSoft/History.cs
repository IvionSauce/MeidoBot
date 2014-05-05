using System;
using System.Collections.Generic;

namespace IvionSoft
{
    public class History<T>
    {
        public int Length { get; private set; }

        HashSet<T> hashes;
        Queue<T> queue;
        object _locker = new object();


        public History(int length) : this(length, null)
        {}

        public History(int length, IEqualityComparer<T> comparer)
        {
            if (length < 1)
                throw new ArgumentException("Must be 1 or larger", "length");
            
            Length = length;
            queue = new Queue<T>(length + 1);

            if (comparer != null)
                hashes = new HashSet<T>(comparer);
            else
                hashes = new HashSet<T>();
        }


        public bool Contains(T item)
        {
            return hashes.Contains(item);
        }

        public bool Add(T item)
        {
            bool added;
            lock (_locker)
            {
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
            }

            return added;
        }
    }
}