using System;

namespace IvionWebSoft
{
    public static class StringExtensions
    {
        public static bool Contains(this string source, string value, StringComparison comp)
        {
            return source.IndexOf(value, comp) >= 0;
        }

        public static void ThrowIfNullOrWhiteSpace(this string source, string argName)
        {
            if (source == null)
                throw new ArgumentNullException(argName);
            else if (source == string.Empty || source.Trim() == string.Empty)
                throw new ArgumentException("Can't be empty or whitespace.", argName);
        }
    }
}