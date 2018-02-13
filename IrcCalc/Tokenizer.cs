using System;
using System.Collections.Generic;


namespace Calculation
{
    class Tokenizer
    {
        [Flags]
        enum TokenTypes
        {
            Number = 1,
            UnaryMinus = 2,
            Operator = 4,
            L_Paren = 8,
            R_Paren = 16,
            Symbol = 32,

            Operand = Number | Symbol,
            ExprBegin = Operand | UnaryMinus | L_Paren,
            ExprEnd = Operator | R_Paren
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
            exprList = new List<string>();
            tmpNum = new List<char>();

            // Set initial values of control and housekeeping variables.
            TokenTypes allowedTokens = TokenTypes.ExprBegin;
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
                        allowedTokens = TokenTypes.Number | TokenTypes.ExprEnd;
                    }
                    else
                        return new TokenizedExpression("Unexpected number: " + c, i);
                }
                else if (c == '.')
                {
                    if (allowedTokens.HasFlag(TokenTypes.Number))
                    {
                        if (decimalPoint)
                            return new TokenizedExpression("More than one decimal point detected", i);

                        AddDotToNum();
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Number;
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
                        AddLParenToExpr();
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.ExprBegin;
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
                            return new TokenizedExpression("Tried to close a subexpression before it was opened", i);

                        AddRParenToExpr();
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.ExprEnd;
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
                        allowedTokens = TokenTypes.ExprBegin;
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
                return new TokenizedExpression("Expression did not end with a number or closing parenthesis");
        }


        void AddDotToNum()
        {
            if (tmpNum.Count == 0)
                tmpNum.Add('0');

            decimalPoint = true;
            tmpNum.Add('.');
        }


        void AddLParenToExpr()
        {
            exprList.Add( "(" );
            // Record the opening of a subexpression to the parentheses balance.
            parenBalance++;
        }

        void AddRParenToExpr()
        {
            // Because a right parenthesis can follow a number, commit collected single digits as a number token.
            AddNumToExpr();

            exprList.Add( ")" );
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
}