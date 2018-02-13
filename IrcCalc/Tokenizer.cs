﻿using System;
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
        // List to temporarily store token characters, for tokens that can be multiple characters long.
        List<char> tmpToken;

        // Keep track if the number has a decimal point, to make sure it only has one.
        bool decimalPoint;
        // CalcToken needs to know the original starting index of tokens, keep track of it for numbers.
        int numberStart;

        // For keeping track of the number of opening and closing parentheses.
        int parenBalance;

        int symbolStart;


        public TokenExpression Tokenize(string expr)
        {
            exprList = new List<CalcToken>();
            tmpToken = new List<char>();

            // Set initial values of control and housekeeping variables.
            TokenTypes allowedTokens = TokenTypes.ExprBegin;
            decimalPoint = false;
            numberStart = -1;
            parenBalance = 0;
            symbolStart = -1;

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

                // ----------------------
                // ----- [7] Symbol -----
                // ----------------------
                else if (allowedTokens.HasFlag(TokenTypes.Symbol))
                {
                    AddCharToSym(c, i);
                    // Set the allowed tokens for the next character.
                    allowedTokens = TokenTypes.Symbol | TokenTypes.Operator | TokenTypes.L_Paren;
                }

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


        // -------------------------------------------
        // --- Methods for single character tokens ---
        // -------------------------------------------

        void AddLParenToExpr(int index)
        {
            // A left parenthesis can follow a function, thus:
            AddFuncToExpr();

            exprList.Add( CalcToken.ParenOpen(index) );
            // Record the opening of a subexpression to the parentheses balance.
            parenBalance++;
        }

        void AddRParenToExpr(int index)
        {
            // A right parenthesis can follow a number or a symbol, thus:
            AddOperandToExpr();

            exprList.Add( CalcToken.ParenClose(index) );
            // Record the closing of a subexpression to the parentheses balance.
            parenBalance--;
        }

        void AddOperatorToExpr(char token, int index)
        {
            // An operator can follow a number or a symbol, thus:
            AddOperandToExpr();

            exprList.Add( CalcToken.Operator(token, index) );
        }


        // -----------------------------------------
        // --- Methods for multicharacter tokens ---
        // -----------------------------------------


        // --- Collecting single chars in temporary storage `tmpToken` ----

        void AddDigitToNum(char c, int index)
        {
            if (tmpToken.Count == 0)
                numberStart = index;

            tmpToken.Add(c);
        }

        void AddDotToNum()
        {
            if (tmpToken.Count == 0)
                tmpToken.Add('0');

            decimalPoint = true;
            tmpToken.Add('.');
        }

        void AddCharToSym(char c, int index)
        {
            if (tmpToken.Count == 0)
                symbolStart = index;

            tmpToken.Add(c);
        }


        // --- Comitting collected characters as a single token ---

        void AddOperandToExpr()
        {
            AddNumToExpr();
            AddSymToExpr();
        }

        void AddNumToExpr()
        {
            if (numberStart >= 0 && tmpToken.Count > 0)
            {
                var num = new string( tmpToken.ToArray() );
                exprList.Add( CalcToken.Number(num, numberStart) );

                tmpToken.Clear();
            }

            decimalPoint = false;
            numberStart = -1;
        }

        void AddSymToExpr(bool isFunction)
        {
            if (symbolStart >= 0 && tmpToken.Count > 0)
            {
                var sym = new string( tmpToken.ToArray() );
                if (isFunction)
                    exprList.Add( CalcToken.Function(sym, symbolStart) );
                else
                    exprList.Add( CalcToken.Symbol(sym, symbolStart) );

                tmpToken.Clear();
            }

            symbolStart = -1;
        }

        void AddSymToExpr()
        {
            AddSymToExpr(false);
        }
        void AddFuncToExpr()
        {
            AddSymToExpr(true);
        }
    }
}