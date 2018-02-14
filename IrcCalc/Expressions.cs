using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace Calculation
{
    public class GenericExpression<T>
    {
        public readonly bool Success;
        public readonly ReadOnlyCollection<T> Expression;
        public readonly string ErrorMessage;
        public readonly int ErrorPosition;

        internal T[] BackingArray;


        public GenericExpression(IEnumerable<T> expr)
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));
            
            Success = true;
            BackingArray = expr.ToArray();
            Expression = new ReadOnlyCollection<T>(BackingArray);
        }

        public GenericExpression(T[] expr)
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));
            
            Success = true;
            BackingArray = expr;
            Expression = new ReadOnlyCollection<T>(BackingArray);
        }


        public GenericExpression(string errorMsg) : this(errorMsg, -1) {}

        public GenericExpression(string errorMsg, int errorPos)
        {
            Success = false;
            ErrorMessage = errorMsg;
            ErrorPosition = errorPos;
        }
    }


    public class TokenExpression : GenericExpression<CalcToken>
    {
        internal TokenExpression(IEnumerable<CalcToken> tokens) : base(tokens) {}

        internal TokenExpression(string errorMsg) : base(errorMsg) {}

        internal TokenExpression(string errorMsg, int errorPos) : base(errorMsg, errorPos) {}


        public static TokenExpression Parse(string expr)
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));
            
            var t = new Tokenizer();
            return t.Tokenize(expr);
        }
    }


    public class VerifiedExpression : GenericExpression<CalcToken>
    {
        internal VerifiedExpression(CalcToken[] tokens) : base(tokens) {}

        internal VerifiedExpression(string errorMsg, int errorPos) : base(errorMsg, errorPos) {}


        public static VerifiedExpression Parse(string expr, CalcEnvironment env)
        {
            if (env == null)
                throw new ArgumentNullException(nameof(env));
            
            var tokenExpr = TokenExpression.Parse(expr);
            if (tokenExpr.Success)
                return Verifier.VerifyExpression(tokenExpr.BackingArray, env);
            else
                return new VerifiedExpression(tokenExpr.ErrorMessage, tokenExpr.ErrorPosition);
        }

        public static VerifiedExpression Create(TokenExpression tokenExpr, CalcEnvironment env)
        {
            if (tokenExpr == null)
                throw new ArgumentNullException(nameof(tokenExpr));
            if (!tokenExpr.Success)
                throw new ArgumentException("Expression must be successfully parsed.", nameof(tokenExpr));
            if (env == null)
                throw new ArgumentNullException(nameof(env));
            
            var arr = tokenExpr.Expression.ToArray();
            return Verifier.VerifyExpression(arr, env);
        }
    }
}
