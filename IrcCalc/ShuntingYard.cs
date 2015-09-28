using System;
using System.Collections.Generic;

namespace Calculation
{
    // For more information: http://en.wikipedia.org/wiki/Shunting-yard_algorithm
    public static class ShuntingYard
    {
        private enum Associativity
        {
            Left,
            Neutral,
            Right
        }

        // First item in the tuple is an int representing the precedence of the operator, second item is the associativity.
        static readonly Dictionary<string, Tuple<int, Associativity>> operators =
            new Dictionary<string, Tuple<int, Associativity>>()
        {
            { "+", Tuple.Create(0, Associativity.Left) },
            { "-", Tuple.Create(0, Associativity.Left) },
            { "*", Tuple.Create(1, Associativity.Left) },
            { "/", Tuple.Create(1, Associativity.Left) },
            { "u-", Tuple.Create(2, Associativity.Right) },
            { "^", Tuple.Create(2, Associativity.Right) }
        };


        public static double Calculate(TokenizedExpression expr)
        {
            if (expr == null)
                throw new ArgumentNullException("expr");
            else if (!expr.Success)
                throw new ArgumentException("Expression must be succesfully parsed.");

            var opStack = new Stack<string>();
            var output = new Stack<double>();

            foreach (string token in expr.Expression)
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


        static int GetPrecedence(string op)
        {
            Tuple<int,Associativity> opInfo;

            if (operators.TryGetValue(op, out opInfo))
                return opInfo.Item1;
            else
                throw new ArgumentException("Operator not supported: " + op, "op");
        }

        static Associativity GetAssociativity(string op)
        {
            Tuple<int,Associativity> opInfo;

            if (operators.TryGetValue(op, out opInfo))
                return opInfo.Item2;
            else
                throw new ArgumentException("Operator not supported: " + op, "op");
        }

        // Return -1 if `op1` has a higher precedence, 0 if equal, and 1 if `op2` has a higher precedence.
        static int ComparePrecedence(string op1, string op2)
        {
            int op1Precedence = GetPrecedence(op1);
            int op2Precedence = GetPrecedence(op2);

            if ( op1Precedence > op2Precedence )
                return -1;
            else if ( op1Precedence < op2Precedence )
                return 1;
            else
                return 0;
        }

        static bool ToPopStack(string incOp, string stackOp)
        {
            // While consuming operators from the stack, make sure it stops short of the left parenthesis.
            if (stackOp == "(")
                return false;

            // If op on the stack has higher precedence, it needs to get popped.
            if (ComparePrecedence(incOp, stackOp) == 1)
                return true;
            // It also needs to get popped when they have equal precedence and the op is left-associative.
            else if (ComparePrecedence(incOp, stackOp) == 0 &&
                     GetAssociativity(incOp) == Associativity.Left)
                return true;
            else
                return false;
        }

        // Does calculation, conform the passed operator, on the output stack and pushes the result back on the stack.
        // It consumes the numbers used for the calculation.
        static void DoCalculation(Stack<double> output, string op)
        {
            double right, left;
            double result;

            switch (op)
            {
            case "+":
                right = output.Pop();
                left = output.Pop();

                result = left + right;
                break;
            case "-":
                right = output.Pop();
                left = output.Pop();

                result = left - right;
                break;
            case "*":
                right = output.Pop();
                left = output.Pop();

                result = left * right;
                break;
            case "/":
                right = output.Pop();
                left = output.Pop();

                result = left / right;
                break;
            case "u-":
                right = output.Pop();

                result = right * -1;
                break;
            case "^":
                right = output.Pop();
                left = output.Pop();

                result = Math.Pow(left, right);
                break;
            default:
                throw new ArgumentException("Operator not supported: " + op, "op");
            }

            output.Push(result);
        }
    }
}