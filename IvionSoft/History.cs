using System;
using System.Collections.Generic;

namespace IvionSoft
{
    public class History<T>
    {
        public int Length { get; private set; }

        readonly HashSet<T> hashes;
        readonly Queue<T> queue;


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


    public class ShortHistory<T>
    {
        public int Length
        {
            get { return items.Length; }
        }

        readonly T[] items;


        public ShortHistory(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", "Cannot be negative.");

            items = new T[length];
        }

        public bool Contains(T item)
        {
            foreach (T entry in items)
            {
                if ( EqualityComparer<T>.Default.Equals(item, entry) )
                    return true;
            }

            return false;
        }

        public bool Add(T item)
        {
            if (Length == 0 || Contains(item))
                return false;

            for (int i = 0; i < (items.Length - 1); i++)
            {
                items[i + 1] = items[i];
            }
            items[0] = item;

            return true;
        }
    }
}