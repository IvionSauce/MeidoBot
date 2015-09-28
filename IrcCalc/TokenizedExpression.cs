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


        internal TokenizedExpression(IEnumerable<string> expr)
        {
            Success = true;
            Expression = new ReadOnlyCollection<string>( expr.ToArray() );
        }

        internal TokenizedExpression(string errorMsg) : this(errorMsg, -1) {}

        internal TokenizedExpression(string errorMsg, int errorPos)
        {
            Success = false;
            ErrorMessage = errorMsg;
            ErrorPosition = errorPos;
        }


        public static TokenizedExpression Parse(string expr)
        {
            var t = new Tokenizer();
            return t.Tokenize(expr);
        }
    }
}