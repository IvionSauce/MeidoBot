using System;
using System.Collections.Generic;

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

        // First item in the tuple is an int representing the precedence of the operator, second item is the associativity.
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
            else if (!expr.Success)
                throw new ArgumentException("Expression must be successfully parsed.");

            var opStack = new Stack<CalcToken>();
            var output = new Stack<double>();

            foreach (var token in expr.Expression)
            {
                if (operators.ContainsKey(token))
                {
                    // First check is to make sure it doesn't try to peek if the opStack is exhausted.
                    while ( opStack.Count != 0 && ToPopStack(token, opStack.Peek()) )
                        DoCalculation(output, opStack.Pop());

                    opStack.Push(token);
                }

                else if (token == "(")
                    opStack.Push(token);

                else if (token == ")")
                {
                    // Keep consuming operators from the opStack until left parenthesis is encountered.
                    while (opStack.Peek() != "(")
                        DoCalculation(output, opStack.Pop());

                    // Operator at the top of the stack is now "(", pop and discard it.
                    opStack.Pop();
                }
                // If not an operator or parenthesis, the remaining option is token being a number. Convert and push
                // onto the output stack.
                else
                {
                    var number = double.Parse(token);
                    output.Push(number);
                }
            }
            // If there are still operators on the opStack, consume them until stack is exhausted.
            while (opStack.Count != 0)
                DoCalculation(output, opStack.Pop());

            // There should be only 1 value left, the final result. Pop and return that.
            return output.Pop();
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

        static bool ToPopStack(OperatorType newOp, OperatorType stackOp)
        {
            // While consuming operators from the stack, make sure it stops short of the left parenthesis.
            //if (stackOp == "(")
            //    return false;

            // If op on the stack has higher precedence, it needs to get popped.
            if (ComparePrecedence(newOp, stackOp) == 1)
                return true;
            // It also needs to get popped when they have equal precedence and the op is left-associative.
            if (ComparePrecedence(newOp, stackOp) == 0 &&
                     GetAssociativity(newOp) == Associativity.Left)
                return true;
            
            return false;
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
    }
}