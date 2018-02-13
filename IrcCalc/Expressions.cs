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


        public GenericExpression(IEnumerable<T> expr)
        {
            if (expr == null)
                throw new System.ArgumentNullException(nameof(expr));
            
            Success = true;
            Expression = new ReadOnlyCollection<T>( expr.ToArray() );
        }

        public GenericExpression(T[] expr)
        {
            if (expr == null)
                throw new System.ArgumentNullException(nameof(expr));
            
            Success = true;
            Expression = new ReadOnlyCollection<T>(expr);
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
    }


    public class VerifiedExpression : GenericExpression<CalcToken>
    {
        internal VerifiedExpression(CalcToken[] tokens) : base(tokens) {}

        internal VerifiedExpression(string errorMsg, int errorPos) : base(errorMsg, errorPos) {}
    }
}
