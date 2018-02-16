using System;
using System.Collections.Generic;
using Calculation.ExtensionMethods;
using Operators = Calculation.ShuntingYardOperators;


namespace Calculation
{
    class SmashTheState
    {
        public int OpCount
        {
            get { return opStack.Count; }
        }

        public bool NextIsFunction
        {
            get { return NextTypeIs(TokenType.Function); }
        }

        readonly Stack<CalcToken> opStack;
        readonly Stack<double> output;
        readonly Action<string, double> assignVariable;


        public SmashTheState() : this(null) {}

        public SmashTheState(Action<string, double> assignVar)
        {
            opStack = new Stack<CalcToken>();
            output = new Stack<double>();
            assignVariable = assignVar;
        }


        public void Push(CalcToken t)
        {
            opStack.Push(t);
        }
        public void Push(double n)
        {
            output.Push(n);
        }

        public CalcToken PopOpStack()
        {
            return opStack.Pop();
        }
        public double PopOutput()
        {
            return output.Pop();
        }

        public TokenType OpPeekType()
        {
            return opStack.PeekType();
        }


        public bool NextTypeIs(TokenType type)
        {
            if (opStack.Count > 0)
                return opStack.PeekType() == type;

            return false;
        }

        // Mostly boilerplate, the heart of the ToPopStack-decision is contained in the next function.
        public bool ToPopStack(CalcOpToken newToken)
        {
            if (opStack.Count > 0)
            {
                var stackToken = opStack.Peek();
                // Only pop operators from the stack.
                if (stackToken.Type == TokenType.Operator)
                {
                    var stackOp = ((CalcOpToken)stackToken).OpType;
                    var newOp = newToken.OpType;
                    return ToPopStack(newOp, stackOp);
                }
            }

            return false;
        }

        static bool ToPopStack(OperatorType newOp, OperatorType stackOp)
        {
            // If op on the stack has higher precedence, it needs to get popped.
            if (Operators.ComparePrecedence(newOp, stackOp) == 1)
                return true;

            // It also needs to get popped when they have equal precedence and the op is left-associative.
            if (Operators.ComparePrecedence(newOp, stackOp) == 0 &&
                Operators.GetAssociativity(newOp) == Operators.Associativity.Left)
                return true;

            return false;
        }
    }
}
