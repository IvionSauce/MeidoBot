using System;
using System.Collections.Generic;

namespace Calculation
{
    public class Tokenizer
    {
        [Flags]
        private enum TokenTypes
        {
            Number = 1,
            UnaryMinus = 2,
            Operator = 4,
            L_Paren = 8,
            R_Paren = 16
        }

        static readonly HashSet<char> opChars =
            new HashSet<char>(new char[] { '+', '-', '*', '/', '^' });

        // List in which to collect the tokenized expression.
        List<string> exprList;

        // List to temporarily store the digits making up a number (may include a decimal point).
        // Also keep track if the number has a decimal point, to make sure it only has one.
        List<char> tmpNum;
        bool decimalPoint;

        // For keeping track of the number of opening and closing parentheses.
        int parenBalance;


        public TokenizedExpression Tokenize(string expr)
        {
            // Initialize new list to store store our tokenized expression in.
            exprList = new List<string>();
            // Initialize new list to store digits/dot in.
            tmpNum = new List<char>();

            // Set initial values of control and housekeeping variables.
            TokenTypes allowedTokens = TokenTypes.Number | TokenTypes.UnaryMinus | TokenTypes.L_Paren;
            decimalPoint = false;
            parenBalance = 0;

            for (int i = 0; i < expr.Length; i++)
            {
                char c = expr[i];

                // ----------------------
                // ----- [1] Number -----
                // ----------------------
                if (char.IsDigit(c))
                {
                    if (allowedTokens.HasFlag(TokenTypes.Number))
                    {
                        tmpNum.Add(c);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Number | TokenTypes.Operator | TokenTypes.R_Paren;
                    }
                    else
                        return new TokenizedExpression("Unexpected number: " + c, i);
                }
                if (c == '.')
                {
                    if (allowedTokens.HasFlag(TokenTypes.Number))
                    {
                        if (decimalPoint)
                            return new TokenizedExpression("More than one decimal point detected", i);

                        decimalPoint = true;
                        tmpNum.Add(c);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Number | TokenTypes.Operator | TokenTypes.R_Paren;
                    }
                    else
                        return new TokenizedExpression("Unexpected decimal point", i);
                }

                // ---------------------------
                // ----- [2] Unary Minus -----
                // ---------------------------
                else if (c == '-' && allowedTokens.HasFlag(TokenTypes.UnaryMinus))
                    exprList.Add("u-");

                // --------------------------------
                // ----- [3] Left Parenthesis -----
                // --------------------------------
                else if (c == '(')
                {
                    if (allowedTokens.HasFlag(TokenTypes.L_Paren))
                    {
                        AddLParenToExpr(c);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Number | TokenTypes.UnaryMinus | TokenTypes.L_Paren;
                    }
                    else
                        return new TokenizedExpression("Unexpected left parenthesis", i);
                }

                // ---------------------------------
                // ----- [4] Right Parenthesis -----
                // ---------------------------------
                else if (c == ')')
                {
                    if (allowedTokens.HasFlag(TokenTypes.R_Paren))
                    {
                        if (parenBalance == 0)
                            return new TokenizedExpression("Tried to close a subexpression before it was opened.", i);

                        AddRParenToExpr(c);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Operator | TokenTypes.R_Paren;
                    }
                    else
                        return new TokenizedExpression("Unexpected right parenthesis", i);
                }

                // ------------------------
                // ----- [5] Operator -----
                // ------------------------
                else if (opChars.Contains(c))
                {
                    if (allowedTokens.HasFlag(TokenTypes.Operator))
                    {
                        AddOperatorToExpr(c);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Number | TokenTypes.UnaryMinus | TokenTypes.L_Paren;
                    }
                    else
                        return new TokenizedExpression("Unexpected operator: " + c, i);
                }

                // ----- [6] Whitespace -----
                // Ignore whitespace.
                else if (char.IsWhiteSpace(c))
                    continue;

                // ----- [7] Unsupported Character -----
                // Abort on unsupported character.
                else
                    return new TokenizedExpression("Unsupported character: " + c, i);
            }

            // Abort on unclosed subexpression(s) / too many left parentheses.
            if (parenBalance > 0)
                return new TokenizedExpression("Unclosed subexpression(s) detected, amount: " + parenBalance);

            AddNumToExpr();

            // Final check. The expression should have ended in either a number or a right parenthesis. Both of these
            // set the allowedTokens to include TokenType.Operator, which would not be set by the others. So check for
            // that to determine whether the expression has ended correctly.
            if (allowedTokens.HasFlag(TokenTypes.Operator))
                return new TokenizedExpression(exprList);
            else
                return new TokenizedExpression("Expression did not end with a number or closing parenthesis.");
        }


        void AddLParenToExpr(char token)
        {
            exprList.Add( token.ToString() );
            // Record the opening of a subexpression to the parentheses balance.
            parenBalance++;
        }


        void AddRParenToExpr(char token)
        {
            // Because a left parenthesis can follow a number, commit collected single digits as a number token.
            AddNumToExpr();

            exprList.Add( token.ToString() );
            // Record the closing of a subexpression to the parentheses balance.
            parenBalance--;
        }


        void AddOperatorToExpr(char token)
        {
            // Because an operator can follow a number, commit collected single digits as a number token.
            AddNumToExpr();

            exprList.Add( token.ToString() );
        }


        void AddNumToExpr()
        {
            if (tmpNum.Count == 0)
                return;
            
            var numString = new string( tmpNum.ToArray() );
            exprList.Add(numString);
            
            tmpNum.Clear();
            decimalPoint = false;
        }
    }


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