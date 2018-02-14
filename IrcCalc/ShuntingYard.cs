using System;
using System.Collections.Generic;
using Calculation.ExtensionMethods;

namespace Calculation
{
    // For more information: http://en.wikipedia.org/wiki/Shunting-yard_algorithm
    public static class ShuntingYard
    {
        enum Associativity
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


        public static double Calculate(VerifiedExpression expr)
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));
            if (!expr.Success)
                throw new ArgumentException("Expression must be successfully parsed.");

            var opStack = new Stack<CalcToken>();
            var output = new Stack<double>();

            foreach (var token in expr.Expression)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                    case TokenType.Symbol:
                    output.Push( ((INumberValue)token).NumberValue );
                    break;

                    case TokenType.Operator:
                    while (ToPopStack(opStack, (CalcOpToken)token))
                    {
                        DoCalculation(output, opStack.Pop());
                    }
                    opStack.Push(token);
                    break;

                    case TokenType.Function:
                    case TokenType.ParenOpen:
                    opStack.Push(token);
                    break;

                    case TokenType.ArgSeperator:
                    // Keep consuming operators from the opStack until left parenthesis is encountered.
                    while (opStack.PeekType() != TokenType.ParenOpen)
                    {
                        DoCalculation(output, opStack.Pop());
                    }
                    // Afterwards don't discard it.
                    break;

                    case TokenType.ParenClose:
                    // Keep consuming operators from the opStack until left parenthesis is encountered,
                    // afterwards discard it.
                    while (opStack.PeekType() != TokenType.ParenOpen)
                    {
                        DoCalculation(output, opStack.Pop());
                    }
                    opStack.Pop();
                    if (opStack.Count != 0 && opStack.PeekType() == TokenType.Function)
                    {
                        DoFunction(output, (CalcFuncToken)token);
                    }
                    break;
                }
            }

            // If there are still operators on the opStack, consume them until stack is exhausted.
            while (opStack.Count != 0)
                DoCalculation(output, opStack.Pop());

            // There should be only 1 value left, the final result. Pop and return that.
            return output.Pop();
        }


        static bool ToPopStack(Stack<CalcToken> opStack, CalcOpToken newToken)
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
            if (ComparePrecedence(newOp, stackOp) == 1)
                return true;
            // It also needs to get popped when they have equal precedence and the op is left-associative.
            if (ComparePrecedence(newOp, stackOp) == 0 &&
                GetAssociativity(newOp) == Associativity.Left)
                return true;

            return false;
        }


        static void DoCalculation(Stack<double> output, CalcToken token)
        {
            switch (token.Type)
            {
                case TokenType.Operator:
                var opToken = (CalcOpToken)token;
                DoCalculation(output, opToken.OpType);
                break;

                case TokenType.Function:
                throw new InvalidOperationException("WAKE ME UP");
                default:
                throw new InvalidOperationException("CAN'T WAKE UP");
            }
        }

        static void DoFunction(Stack<double> output, CalcFuncToken token)
        {
            var func = token.Func;
            double[] args = new double[func.ArgCount];
            for (int i = 0; i < func.ArgCount; i++)
            {
                args[i] = output.Pop();
            }
            output.Push(func.Apply(args));
        }

        // Does calculation, conform the passed operator, on the output stack and pushes the result back on the stack.
        // It consumes the numbers used for the calculation.
        static void DoCalculation(Stack<double> output, OperatorType op)
        {
            double right, left;
            double result;

            switch (op)
            {
                case OperatorType.Add:
                right = output.Pop();
                left = output.Pop();
                result = left + right;
                break;

                case OperatorType.Sub:
                right = output.Pop();
                left = output.Pop();
                result = left - right;
                break;

                case OperatorType.Mult:
                right = output.Pop();
                left = output.Pop();
                result = left * right;
                break;

                case OperatorType.Div:
                right = output.Pop();
                left = output.Pop();
                result = left / right;
                break;

                case OperatorType.UnaryMinus:
                right = output.Pop();
                result = right * -1;
                break;

                case OperatorType.Pow:
                right = output.Pop();
                left = output.Pop();
                result = Math.Pow(left, right);
                break;

            default:
                throw new ArgumentException("Operator not supported: " + op, nameof(op));
            }

            output.Push(result);
        }


        // ----- Helper functions -----

        // Return -1 if `op1` has a higher precedence, 0 if equal, and 1 if `op2` has a higher precedence.
        static int ComparePrecedence(OperatorType op1, OperatorType op2)
        {
            int op1Precedence = GetPrecedence(op1);
            int op2Precedence = GetPrecedence(op2);

            if ( op1Precedence > op2Precedence )
                return -1;
            if ( op1Precedence < op2Precedence )
                return 1;

            return 0;
        }

        static int GetPrecedence(OperatorType op)
        {
            Tuple<int,Associativity> opInfo;

            if (operators.TryGetValue(op, out opInfo))
                return opInfo.Item1;
            else
                throw new ArgumentException("Operator not supported: " + op, nameof(op));
        }

        static Associativity GetAssociativity(OperatorType op)
        {
            Tuple<int,Associativity> opInfo;

            if (operators.TryGetValue(op, out opInfo))
                return opInfo.Item2;
            else
                throw new ArgumentException("Operator not supported: " + op, nameof(op));
        }
    }
}