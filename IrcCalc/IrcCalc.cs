using System.Collections.Generic;
using MeidoCommon.Parsing;
using Calculation;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class Calc : IMeidoHook, IPluginTriggers, IPluginIrcHandlers
{
    public string Name
    {
        get { return "IrcCalc"; }
    }
    public string Version
    {
        get { return "2.0"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }
    public IEnumerable<IIrcHandler> IrcHandlers { get; private set; }


    CalcEnvironment CalcEnv = new CalcEnvironment();


    public void Stop()
    {}

    [ImportingConstructor]
    public Calc(IIrcComm irc, IMeidoComm meido)
    {
        Triggers = Trigger.Group(
            
            new Trigger(HandleTrigger, "calc", "c") {
                Help = new TriggerHelp(
                    "<expression>",
                    "Evaluates mathematical expression. Accepted operators are: addition (+), " +
                    "substraction (-), multiplication (*), division (/) and exponentiation (^ and **).")
            },

            new Trigger(DefVar, "defvar", "var") {
                Help = new TriggerHelp(
                    "<variable> <expression>",
                    "Evaluates and assigns expression to variable.")
            }
        );

        IrcHandlers = new IIrcHandler[] {
            new IrcHandler<IQueryMsg>(HandleMessage)
        };
    }


    void HandleTrigger(ITriggerMsg e)
    {
        var exprStr = e.MessageWithoutTrigger();
        if (exprStr.HasValue())
        {
            var expr = VerifiedExpression.Parse(exprStr, CalcEnv);

            if (expr.Success)
            {
                double result = ShuntingYard.Calculate(expr);
                e.Reply( result.ToString() );
            }
            else
                OutputError(e, expr);
        }
    }


    void HandleMessage(IQueryMsg e)
    {
        var expr = VerifiedExpression.Parse(e.Message, CalcEnv);
        // Only automatically calculate if the expression is legitimate and if it's reasonable to assume it's meant
        // to be a calculation (not just a single number). A minimal calculation will involve at least 3 tokens:
        // `number operator number`.
        const int minTokenCount = 3;
        if (expr.Success && expr.Expression.Count >= minTokenCount)
        {
            double result = ShuntingYard.Calculate(expr);
            e.Reply( result.ToString() );
        }
    }


    static void OutputError<T>(ITriggerMsg e, GenericExpression<T> expr)
    {
        string error = expr.ErrorMessage;
        if (expr.ErrorPosition >= 0)
            error += " | Postion: " + expr.ErrorPosition;

        e.Reply(error);
    }


    void DefVar(ITriggerMsg e)
    {
        var expression = e.GetArg(out string symbol).ToJoined();

        if (ParseArgs.Success(symbol, expression))
        {
            var expr = VerifiedExpression.Parse(expression, CalcEnv);
            if (CheckPreconditions(e, expr, symbol))
            {
                double result = ShuntingYard.Calculate(expr);
                if (CalcEnv.Variable(symbol, out double previous))
                {
                    e.Reply("{0} = {1} (Previous value: {2})", symbol, result, previous);
                }
                else
                    e.Reply("{0} = {1}", symbol, result);

                CalcEnv.AssignVariable(symbol, result);
            }
        }
    }

    static bool CheckPreconditions(ITriggerMsg e, VerifiedExpression expr, string symbol)
    {
        if (!expr.Success)
        {
            OutputError(e, expr);
            return false;
        }
        if (!Verifier.VerifySymbol(symbol))
        {
            e.Reply("Symbol contains illegal characters.");
            return false;
        }

        return true;
    }
}