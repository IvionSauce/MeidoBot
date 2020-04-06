using System;
using System.Linq;
using System.Collections.Generic;


namespace MeidoCommon.ExtensionMethods
{
    public static class ExtensionMethods
    {
        public static IEnumerable<T> NoNull<T>(this IEnumerable<T> seq) where T : class
        {
            if (seq != null)
                return seq.Where(el => el != null);
            else
                return Enumerable.Empty<T>();
        }


        public static T[] Subarray<T>(this IList<T> items, int start)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (start > items.Count)
                throw new ArgumentOutOfRangeException(nameof(start), "Cannot be greater than items' count.");

            return Subarray(items, start, items.Count - start);
        }

        public static T[] Subarray<T>(this IList<T> items, int start, int count)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), "Cannot be negative.");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot be negative.");
            if ( (start + count) > items.Count )
                throw new ArgumentException("Requested range would go out of bounds.");
            // Early return.
            if (count == 0)
                return Array.Empty<T>();

            var retval = new T[count];
            // If items is actually an array use the efficient Array.Copy.
            if (items is T[] arr)
            {
                Array.Copy(arr, start, retval, 0, count);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    retval[i] = items[start + i];
                }
            }

            return retval;
        }
    }
}