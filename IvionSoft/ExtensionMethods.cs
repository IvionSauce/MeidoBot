using System;
using System.Collections.Generic;

namespace IvionSoft
{
    public static class ExtensionMethods
    {
        public static bool Contains(this string source, string value, StringComparison comp)
        {
            return source.IndexOf(value, comp) >= 0;
        }


        // Fisher–Yates shuffle
        // http://stackoverflow.com/questions/108819/
        public static void Shuffle<T> (this Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1) 
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }


        // http://www.dotnetperls.com/array-slice
        // Modification: allow `end` to be 0, this will mean it will slice from `start` to whatever length the array is.
        // This will allow you to have an inclusive end; -1 slices off the last item and 0 would cause an error
        // (without this modification).
        public static T[] Slice<T>(this T[] source, int start, int end)
        {
            // Handles negative ends.
            if (end <= 0)
                end = source.Length + end;

            int len = end - start;
            
            // Return new array.
            T[] res = new T[len];
            for (int i = 0; i < len; i++)
                res[i] = source[i + start];

            return res;
        }


        // http://www.dotnetperls.com/punctuation
        public static string TrimPunctuation(this string value)
        {
            // Count start punctuation.
            int removeFromStart = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsPunctuation(value[i]))
                    removeFromStart++;
                else
                    break;
            }
            // Count end punctuation.
            int removeFromEnd = 0;
            for (int i = (value.Length - 1); i >= 0; i--)
            {
                if (char.IsPunctuation(value[i]))
                    removeFromEnd++;
                else
                    break;
            }

            // No characters were punctuation.
            if (removeFromStart == 0 && removeFromEnd == 0)
                return value;
            // All characters were punctuation.
            else if (removeFromStart == value.Length && removeFromEnd == value.Length)
                return string.Empty;
            // Substring.
            else
            {
                int len = value.Length - removeFromEnd - removeFromStart;
                return value.Substring(removeFromStart, len);
            }
        }


        public static void ThrowIfNullOrEmpty(this string source, string argName)
        {
            switch (source)
            {
            case null:
                throw new ArgumentNullException(argName);
            case "":
                throw new ArgumentException("Can't be empty.", argName);
            default:
                return;
            }                
        }

        public static void ThrowIfNullOrWhiteSpace(this string source, string argName)
        {
            if (source == null)
                throw new ArgumentNullException(argName);
            else if (source == string.Empty || source.Trim() == string.Empty)
                throw new ArgumentException("Can't be empty or whitespace.", argName);
        }

        public static bool IsEmptyOrWhiteSpace(this string source)
        {
            if (source == string.Empty || source.Trim() == string.Empty)
                return true;
            else
                return false;
        }


        public static V GetOrAdd<K,V>(this Dictionary<K,V> source, K key) where V: class, new()
        {
            V item;
            if (!source.TryGetValue(key, out item))
            {
                item = new V();
                source.Add(key, item);
            }
            return item;
        }


        public static T ChooseRndItem<T>(this Random rnd, IList<T> items)
        {
            int rndIndex = rnd.Next(items.Count);
            return items[rndIndex];
        }

    }
}