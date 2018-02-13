using System;
using System.Collections.Generic;
using Calculation.ExtensionMethods;


namespace Calculation
{
    public class CalcEnvironment
    {
        static readonly Dictionary<string, double> constants;
        readonly Dictionary<string, double> variables;
        readonly Dictionary<string, CalcFunction> functions;


        static CalcEnvironment()
        {
            constants = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                {"pi", Math.PI},
                {"π", Math.PI},
                {"e", Math.E},
                {"φ", (1 + Math.Sqrt(5)) / 2}
            };
        }


        public CalcEnvironment()
        {
            variables = new Dictionary<string, double>(StringComparer.Ordinal);

            functions = new Dictionary<string, CalcFunction>(StringComparer.Ordinal)
            {
                {"sqrt",
                    new CalcFunction( 1, args => Math.Sqrt(args[0]) )},
                {"ln",
                    new CalcFunction( 1, args => Math.Log(args[0]) )},
                {"log10",
                    new CalcFunction( 1, args => Math.Log10(args[0]) )},
                {"log",
                    new CalcFunction( 2, args => Math.Log(args[0], args[1]) )},
                {"root",
                    new CalcFunction( 2, args => Math.Pow(args[0], 1d/args[1]) )}
            };
        }


        public bool VarOrConst(string symbol, out double number)
        {
            symbol.ThrowIfNullOrWhiteSpace(nameof(symbol));
            
            // Variables shadow constants.
            if (variables.TryGetValue(symbol, out number) ||
                constants.TryGetValue(symbol, out number))
                return true;
            
            return false;
        }

        public bool Function(string symbol, out CalcFunction function)
        {
            symbol.ThrowIfNullOrWhiteSpace(nameof(symbol));
            
            if (functions.TryGetValue(symbol, out function))
                return true;

            return false;
        }


        public double AssignVariable(string symbol, double number)
        {
            symbol.ThrowIfNullOrWhiteSpace(nameof(symbol));
            
            variables[symbol] = number;
            return number;
        }

        public string AssignFunction(string symbol, CalcFunction function)
        {
            symbol.ThrowIfNullOrWhiteSpace(nameof(symbol));
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            
            functions[symbol] = function;
            return symbol;
        }
    }


    public class CalcFunction
    {
        public delegate double Func(params double[] args);
        public readonly int ArgCount;
        readonly Func f;


        public CalcFunction(int argcount, Func function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            
            ArgCount = argcount;
            f = function;
        }

        public double Apply(params double[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            
            if (args.Length != ArgCount)
            {
                throw new ArgumentException(
                    string.Format("Got {0} arguments, expected {1}.", args.Length, ArgCount),
                    nameof(args));
            }

            return f(args);
        }
    }
}