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

        // List in which to collect the tokenized expression.
        List<CalcToken> exprList;

        // List to temporarily store the digits making up a number (may include a decimal point).
        // Also keep track if the number has a decimal point, to make sure it only has one.
        List<char> tmpNum;
        bool decimalPoint;
        // CalcToken needs to know the original starting index of tokens, keep track of it for numbers.
        int numberStart;

        // For keeping track of the number of opening and closing parentheses.
        int parenBalance;


        public TokenExpression Tokenize(string expr)
        {
            exprList = new List<CalcToken>();
            tmpNum = new List<char>();

            // Set initial values of control and housekeeping variables.
            TokenTypes allowedTokens = TokenTypes.ExprBegin;
            decimalPoint = false;
            numberStart = -1;
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
                        AddDigitToNum(c, i);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Number | TokenTypes.ExprEnd;
                    }
                    else
                        return new TokenExpression("Unexpected number: " + c, i);
                }
                else if (c == '.')
                {
                    if (allowedTokens.HasFlag(TokenTypes.Number))
                    {
                        if (decimalPoint)
                            return new TokenExpression("More than one decimal point detected", i);

                        AddDotToNum();
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.Number;
                    }
                    else
                        return new TokenExpression("Unexpected decimal point", i);
                }

                // ---------------------------
                // ----- [2] Unary Minus -----
                // ---------------------------
                else if (c == '-' && allowedTokens.HasFlag(TokenTypes.UnaryMinus))
                    exprList.Add( CalcToken.UnaryMinus(i) );

                // --------------------------------
                // ----- [3] Left Parenthesis -----
                // --------------------------------
                else if (c == '(')
                {
                    if (allowedTokens.HasFlag(TokenTypes.L_Paren))
                    {
                        AddLParenToExpr(i);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.ExprBegin;
                    }
                    else
                        return new TokenExpression("Unexpected left parenthesis", i);
                }

                // ---------------------------------
                // ----- [4] Right Parenthesis -----
                // ---------------------------------
                else if (c == ')')
                {
                    if (allowedTokens.HasFlag(TokenTypes.R_Paren))
                    {
                        if (parenBalance == 0)
                            return new TokenExpression("Tried to close a subexpression before it was opened", i);

                        AddRParenToExpr(i);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.ExprEnd;
                    }
                    else
                        return new TokenExpression("Unexpected right parenthesis", i);
                }

                // ------------------------
                // ----- [5] Operator -----
                // ------------------------
                else if (CalcToken.IsOperatorChar(c))
                {
                    if (allowedTokens.HasFlag(TokenTypes.Operator))
                    {
                        AddOperatorToExpr(c, i);
                        // Set the allowed tokens for the next character.
                        allowedTokens = TokenTypes.ExprBegin;
                    }
                    else
                        return new TokenExpression("Unexpected operator: " + c, i);
                }

                // ----- [6] Whitespace -----
                // Ignore whitespace.
                else if (char.IsWhiteSpace(c))
                    continue;

                // ----- [7] Unsupported Character -----
                // Abort on unsupported character.
                else
                    return new TokenExpression("Unsupported character: " + c, i);
            }

            // Abort on unclosed subexpression(s) / too many left parentheses.
            if (parenBalance > 0)
                return new TokenExpression("Unclosed subexpression(s) detected, amount: " + parenBalance);

            AddNumToExpr();

            // Final check. The expression should have ended in either a number or a right parenthesis. Both of these
            // set the allowedTokens to include TokenType.Operator, which would not be set by the others. So check for
            // that to determine whether the expression has ended correctly.
            if (allowedTokens.HasFlag(TokenTypes.Operator))
                return new TokenExpression(exprList);
            else
                return new TokenExpression("Expression did not end with a number or closing parenthesis");
        }


        void AddDigitToNum(char c, int index)
        {
            if (tmpNum.Count == 0)
                numberStart = index;

            tmpNum.Add(c);
        }

        void AddDotToNum()
        {
            if (tmpNum.Count == 0)
                tmpNum.Add('0');

            decimalPoint = true;
            tmpNum.Add('.');
        }


        void AddLParenToExpr(int index)
        {
            exprList.Add( CalcToken.ParenOpen(index) );
            // Record the opening of a subexpression to the parentheses balance.
            parenBalance++;
        }

        void AddRParenToExpr(int index)
        {
            // Because a right parenthesis can follow a number, commit collected single digits as a number token.
            AddNumToExpr();

            exprList.Add( CalcToken.ParenClose(index) );
            // Record the closing of a subexpression to the parentheses balance.
            parenBalance--;
        }

        void AddOperatorToExpr(char token, int index)
        {
            // Because an operator can follow a number, commit collected single digits as a number token.
            AddNumToExpr();

            exprList.Add( CalcToken.Operator(token, index) );
        }


        void AddNumToExpr()
        {
            if (tmpNum.Count == 0)
                return;

            var num = new string( tmpNum.ToArray() );
            exprList.Add( CalcToken.Number(num, numberStart) );

            tmpNum.Clear();
            decimalPoint = false;
            numberStart = -1;
        }
    }
}