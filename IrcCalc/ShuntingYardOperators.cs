using System;
using System.Collections.Generic;


namespace Calculation
{
    public static class ShuntingYardOperators
    {
        public enum Associativity
        {
            Left,
            Neutral,
            Right
        }

        // First item in the tuple is an int representing the precedence of the operator,
        // second item is the associativity.
        static readonly Dictionary<OperatorType, Tuple<int, Associativity>> operators =
            new Dictionary<OperatorType, Tuple<int, Associativity>>()
        {
            { OperatorType.Add,         Tuple.Create(0, Associativity.Left) },
            { OperatorType.Sub,         Tuple.Create(0, Associativity.Left) },
            { OperatorType.Mult,        Tuple.Create(1, Associativity.Left) },
            { OperatorType.Div,         Tuple.Create(1, Associativity.Left) },
            { OperatorType.UnaryMinus,  Tuple.Create(2, Associativity.Right) },
            { OperatorType.Pow,         Tuple.Create(2, Associativity.Right) }
        };


        // Return -1 if `op1` has a higher precedence, 0 if equal, and 1 if `op2` has a higher precedence.
        public static int ComparePrecedence(OperatorType op1, OperatorType op2)
        {
            int op1Precedence = GetPrecedence(op1);
            int op2Precedence = GetPrecedence(op2);

            if ( op1Precedence > op2Precedence )
                return -1;
            if ( op1Precedence < op2Precedence )
                return 1;

            return 0;
        }

        public static int GetPrecedence(OperatorType op)
        {
            Tuple<int,Associativity> opInfo;

            if (operators.TryGetValue(op, out opInfo))
                return opInfo.Item1;
            else
                throw new ArgumentException("Operator not supported: " + op, nameof(op));
        }

        public static Associativity GetAssociativity(OperatorType op)
        {
            Tuple<int,Associativity> opInfo;

            if (operators.TryGetValue(op, out opInfo))
                return opInfo.Item2;
            else
                throw new ArgumentException("Operator not supported: " + op, nameof(op));
        }
    }
}