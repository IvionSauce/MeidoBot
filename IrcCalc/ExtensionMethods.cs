using System;
using System.Collections.Generic;


namespace Calculation.ExtensionMethods
{
    public static class ExtensionMethods
    {
        public static void ThrowIfNullOrWhiteSpace(this string source, string argName)
        {
            if (source == null)
                throw new ArgumentNullException(argName);
            else if (source == string.Empty || source.Trim() == string.Empty)
                throw new ArgumentException("Can't be empty or whitespace.", argName);
        }


        public static TokenType PeekType(this Stack<CalcToken> source)
        {
            return source.Peek().Type;
        }
    }
}