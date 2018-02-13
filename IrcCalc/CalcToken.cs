﻿using System;
using System.Collections.Generic;
using Calculation.ExtensionMethods;


namespace Calculation
{
    public enum TokenType
    {
        Number,
        Operator,
        ParenOpen,
        ParenClose,
        ArgSeperator,
        Function,
        Symbol
    }

    public enum OperatorType
    {
        UnaryMinus,
        Add,
        Sub,
        Mult,
        Div,
        Pow
    }


    // ---------------------
    // --- Generic Token ---
    // ---------------------

    public class CalcToken
    {
        public readonly TokenType Type;
        public readonly string Value;
        public readonly int OriginIndex;

        static readonly HashSet<char> opChars = new HashSet<char>("+-*/^");


        public CalcToken(TokenType t, string val, int originIdx)
        {
            val.ThrowIfNullOrWhiteSpace(nameof(val));
            
            Type = t;
            Value = val;
            OriginIndex = originIdx;
        }


        // --- Number Constructor ---

        public static CalcNumberToken Number(string num, int originIndex)
        {
            num.ThrowIfNullOrWhiteSpace(nameof(num));
            
            double number;
            if (double.TryParse(num, out number))
                return new CalcNumberToken(number, num, originIndex);
            else
                throw new ArgumentException("Not a valid number: " + num, nameof(num));
        }


        // --- Operator Constructors ---

        public static CalcOpToken UnaryMinus(int originIndex)
        {
            return new CalcOpToken(OperatorType.UnaryMinus, "-", originIndex);
        }

        public static CalcOpToken Operator(char op, int originIndex)
        {
            OperatorType opType;
            switch (op)
            {
                case '+':
                opType = OperatorType.Add;
                break;
                case '-':
                opType = OperatorType.Sub;
                break;
                case '*':
                opType = OperatorType.Mult;
                break;
                case '/':
                opType = OperatorType.Div;
                break;
                case '^':
                opType = OperatorType.Pow;
                break;
                default:
                throw new ArgumentException("Not a valid operator: " + op, nameof(op));
            }

            return new CalcOpToken(opType, op.ToString(), originIndex);
        }

        public static CalcOpToken Operator(string op, int originIndex)
        {
            op.ThrowIfNullOrWhiteSpace(nameof(op));
            
            if (op.Length > 1)
            {
                OperatorType opType;
                switch (op)
                {
                    case "**":
                    opType = OperatorType.Pow;
                    break;
                    default:
                    throw new ArgumentException("Not a valid operator: " + op, nameof(op));
                }

                return new CalcOpToken(opType, op, originIndex);
            }
            else
                return Operator(op[0], originIndex);
        }


        // --- Misc Generic Constructors ---

        public static CalcToken ParenOpen(int originIndex)
        {
            return new CalcToken(TokenType.ParenOpen, "(", originIndex);
        }

        public static CalcToken ParenClose(int originIndex)
        {
            return new CalcToken(TokenType.ParenClose, ")", originIndex);
        }

        public static CalcToken ArgSeperator(int originIndex)
        {
            return new CalcToken(TokenType.ArgSeperator, ",", originIndex);
        }


        // --- Function & Symbol Constructors ---

        public static CalcFuncToken Function(string symbol, int originIndex)
        {
            symbol.ThrowIfNullOrWhiteSpace(nameof(symbol));
            
            return new CalcFuncToken(symbol, originIndex);
        }

        public static CalcSymbolToken Symbol(string symbol, int originIndex)
        {
            symbol.ThrowIfNullOrWhiteSpace(nameof(symbol));
            
            return new CalcSymbolToken(symbol, originIndex);
        }


        // --- Helper Functions ---

        public static bool IsOperatorChar(char c)
        {
            return opChars.Contains(c);
        }

        public static bool IsOperator(string op)
        {
            op.ThrowIfNullOrWhiteSpace(nameof(op));

            if (op.Length == 1)
                return IsOperatorChar(op[0]);
            if (op == "**")
                return true;

            return false;
        }
    }


    // -----------------------
    // --- Specific Tokens ---
    // -----------------------

    public class CalcNumberToken : CalcToken
    {
        public readonly double NumberValue;


        public CalcNumberToken(double num, string val, int originIdx) :
        base(TokenType.Number, val, originIdx)
        {
            NumberValue = num;
        }
    }


    public class CalcOpToken : CalcToken
    {
        public readonly OperatorType OpType;


        public CalcOpToken(OperatorType op, string val, int originIdx) :
        base(TokenType.Operator, val, originIdx)
        {
            OpType = op;
        }

    }


    public class CalcFuncToken : CalcToken
    {
        public readonly CalcFunction Func;


        public CalcFuncToken(string val, int originIdx) :
        this(null, val, originIdx) {}

        public CalcFuncToken(CalcFunction func, string val, int originIdx) :
        base(TokenType.Function, val, originIdx)
        {
            Func = func;
        }


        public CalcFuncToken New(CalcFunction func)
        {
            return new CalcFuncToken(func, Value, OriginIndex);
        }
    }


    public class CalcSymbolToken : CalcToken
    {
        public readonly double? NumberValue;


        public CalcSymbolToken(string val, int originIdx) :
        base(TokenType.Symbol, val, originIdx) {}

        public CalcSymbolToken(double num, string val, int originIdx) :
        base(TokenType.Symbol, val, originIdx)
        {
            NumberValue = num;
        }


        public CalcSymbolToken New(double num)
        {
            return new CalcSymbolToken(num, Value, OriginIndex);
        }
    }
}