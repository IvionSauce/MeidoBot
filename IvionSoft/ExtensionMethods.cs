using System;

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
        public static T[] Slice<T>(this T[] source, int start, int end)
        {
            // Handles negative ends.
            if (end < 0)
            {
                end = source.Length + end;
            }
            int len = end - start;
            
            // Return new array.
            T[] res = new T[len];
            for (int i = 0; i < len; i++)
            {
                res[i] = source[i + start];
            }
            return res;
        }

        public static void ThrowIfNullOrEmpty(this string source, string argName)
        {
            if (source == null)
                throw new ArgumentNullException(argName);
            else if (source == string.Empty)
                throw new ArgumentException("Can't be empty.", argName);
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
    }
}