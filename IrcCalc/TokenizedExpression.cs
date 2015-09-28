using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace Calculation
{
    public class TokenizedExpression
    {
        public readonly bool Success;
        public readonly ReadOnlyCollection<string> Expression;
        public readonly string ErrorMessage;
        public readonly int ErrorPosition;


        public TokenizedExpression(IEnumerable<string> expr)
        {
            Success = true;
            Expression = new ReadOnlyCollection<string>( expr.ToArray() );
        }

        public TokenizedExpression(string errorMsg) : this(errorMsg, -1) {}

        public TokenizedExpression(string errorMsg, int errorPos)
        {
            Success = false;
            ErrorMessage = errorMsg;
            ErrorPosition = errorPos;
        }
    }
}