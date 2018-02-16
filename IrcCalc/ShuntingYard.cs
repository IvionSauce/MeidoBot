using System;
using System.Collections.Generic;
using Calculation.ExtensionMethods;

namespace Calculation
{
    // For more information: http://en.wikipedia.org/wiki/Shunting-yard_algorithm
    public static class ShuntingYard
    {
        public static double Calculate(VerifiedExpression expr)
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));
            if (!expr.Success)
                throw new ArgumentException("Expression must be successfully parsed.");

            var state = new SmashTheState(); // But not really :(

            foreach (var token in expr.Expression)
            {
                switch (token.Type)
                {
                    // --- Simple pushing cases ---

                    case TokenType.Number:
                    case TokenType.Symbol:
                    state.Push( ((INumberValue)token).NumberValue );
                    break;

                    case TokenType.Function:
                    case TokenType.ParenOpen:
                    state.Push(token);
                    break;

                    // --- Do the ShuntingYard ---

                    // Apply the operators as described by the algorithm, see `ToPopStack` for more details.
                    case TokenType.Operator:
                    while ( state.ToPopStack((CalcOpToken)token) )
                    {
                        DoCalculation(state);
                    }
                    state.Push(token);
                    break;

                    // Behaves like a closing parenthesis, but don't discard the opening parenthesis.
                    case TokenType.ArgSeperator:
                    ConsumeTillOpenParen(state, false);
                    break;

                    // Apply all the remaining operators until we encounter the opening parenthesis,
                    // discard it afterwards.
                    case TokenType.ParenClose:
                    ConsumeTillOpenParen(state, true);
                    // If after that the top of the stack is a function, apply it.
                    if (state.NextIsFunction)
                        DoFunction(state);
                    break;
                }
            }
            // If there are still operators on the opStack, consume them until stack is exhausted.
            while (state.OpCount > 0)
                DoCalculation(state);

            // There should be only 1 value left, the final result. Pop and return that.
            return state.PopOutput();
        }

        static void ConsumeTillOpenParen(SmashTheState state, bool discardParen)
        {
            while (state.OpPeekType() != TokenType.ParenOpen)
            {
                DoCalculation(state);
            }
            if (discardParen)
                state.PopOpStack();
        }


        static void DoCalculation(SmashTheState state)
        {
            var token = state.PopOpStack();
            switch (token.Type)
            {
                case TokenType.Operator:
                var opToken = (CalcOpToken)token;
                DoCalculation(state, opToken.OpType);
                break;

                case TokenType.Function:
                throw new InvalidOperationException("WAKE ME UP");
                default:
                throw new InvalidOperationException("CAN'T WAKE UP");
            }
        }

        static void DoFunction(SmashTheState state)
        {
            var token = (CalcFuncToken)state.PopOpStack();
            var func = token.Func;

            double[] args = new double[func.ArgCount];
            // Reverse, since the first out was the last argument in.
            for (int i = func.ArgCount - 1; i >= 0; i--)
            {
                args[i] = state.PopOutput();
            }
            state.Push(func.Apply(args));
        }

        static void DoCalculation(SmashTheState state, OperatorType op)
        {
            double result;

            if (op != OperatorType.UnaryMinus)
            {
                double right = state.PopOutput();
                double left = state.PopOutput();
                switch (op)
                {
                    case OperatorType.Add:
                    result = left + right;
                    break;

                    case OperatorType.Sub:
                    result = left - right;
                    break;

                    case OperatorType.Mult:
                    result = left * right;
                    break;

                    case OperatorType.Div:
                    result = left / right;
                    break;

                    case OperatorType.Pow:
                    result = Math.Pow(left, right);
                    break;

                    default:
                    throw new ArgumentException("Operator not supported: " + op, nameof(op));
                }
            }
            // OperatorType.UnaryMinus
            else
                result = state.PopOutput() * -1;

            state.Push(result);
        }
    }
}