using System;
using System.Collections.Generic;


namespace Calculation
{
    static class Verifier
    {
        public static VerifiedExpression VerifyExpression(CalcToken[] tokens, CalcEnvironment env)
        {
            int currentDepth = 0;
            // Depth/subexpression -> Argument counter & function metadata.
            // A stack based solution might be better?
            var funcTracking = new Dictionary<int, FunctionMetadata>();

            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                // Used as `out` argument in processing types ArgSeperator and ParenClose.
                // The compiler complains if we define it in both cases, which are counted as the same scope.
                FunctionMetadata funcMeta;

                switch (token.Type)
                {
                    // --- Verify symbol (variables and constants) ---

                    case TokenType.Symbol:
                    double numVal;
                    if (env.VarOrConst(token.Value, out numVal))
                    {
                        // Resolve symbol to value.
                        var symbol = (CalcSymbolToken)token;
                        tokens[i] = symbol.New(numVal);
                        break;
                    }
                    else
                    {
                        return new VerifiedExpression("Undefined symbol: " + token.Value,
                                                      token.OriginIndex);
                    }


                    // --- Verify function symbol ---

                    case TokenType.Function:
                    CalcFunction func;
                    if (env.Function(token.Value, out func))
                    {
                        // Resolve function symbol to function.
                        var funcToken = (CalcFuncToken)token;
                        tokens[i] = funcToken.New(func);

                        // Record that the next subexpression (delimited by parentheses) is associated
                        // with a function.
                        funcTracking[currentDepth + 1] = new FunctionMetadata(func, funcToken);
                        break;
                    }
                    else
                    {
                        return new VerifiedExpression("Undefined function symbol: " + token.Value,
                                                      token.OriginIndex);
                    }


                    // --- Keep count of arguments to functions ---

                    case TokenType.ArgSeperator:
                    if (funcTracking.TryGetValue(currentDepth, out funcMeta))
                    {
                        // Another seperator, another function argument.
                        // The previous stage assures that there will be an operand.
                        funcMeta.ArgCounter++;
                    }
                    break;


                    // --- Record opening and closing of parentheses ---

                    case TokenType.ParenOpen:
                    currentDepth++;
                    break;

                    case TokenType.ParenClose:
                    // If the subexpression we're closing/leaving is associated with a function we need to
                    // verify the function argument count.
                    if (funcTracking.TryGetValue(currentDepth, out funcMeta))
                    {
                        if (!funcMeta.CountsAreEqual)
                        {
                            // And this is why we kept around that metadata, so we could report the appropriate
                            // information on encountering an discrepancy in argument count.
                            return new VerifiedExpression(
                                string.Format("Invalid number of arguments for {0} (got {1} expected {2})",
                                              funcMeta.FuncSymbol, funcMeta.ArgCounter, funcMeta.RequiredArgs),
                                funcMeta.OriginIndex);
                        }

                        // We're leaving the current depth, we no longer need this information.
                        funcTracking.Remove(currentDepth);
                    }
                    currentDepth--;
                    break;


                    // --- The rest is not important for this stage ---
                    default:
                    continue;

                } // switch
            } // for

            // We have successfully stepped through the expression without encountering undefined symbols or functions
            // and the functions were supplied the correct number of arguments. Hurray!
            return new VerifiedExpression(tokens);
        }


        public static bool VerifySymbol(string symbol)
        {
            // Cannot contain certain reserverd characters. Or whitespace.
            foreach (char c in symbol)
            {
                if (CalcToken.IsOperatorChar(c) ||
                    char.IsWhiteSpace(c))
                {
                    return false;
                }

                switch (c)
                {
                    case '(':
                    case ')':
                    case '.':
                    case ',':
                    return false;
                }
            }
            // It also can't be empty or start with a digit.
            if (symbol.Length > 0 && !char.IsDigit(symbol[0]))
                return true;

            return false;
        }
    }


    class FunctionMetadata
    {
        public int ArgCounter;

        readonly CalcFunction func;
        readonly CalcFuncToken token;

        public int RequiredArgs
        {
            get { return func.ArgCount; }
        }

        public string FuncSymbol
        {
            get { return token.Value; }
        }

        public int OriginIndex
        {
            get { return token.OriginIndex; }
        }

        public bool CountsAreEqual
        {
            get { return ArgCounter == func.ArgCount; }
        }


        public FunctionMetadata(CalcFunction func, CalcFuncToken token)
        {
            ArgCounter = 1;
            this.func = func;
            this.token = token;
        }
    }
}