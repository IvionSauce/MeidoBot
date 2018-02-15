using System.Collections.Generic;
using Calculation;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class Calc : IMeidoHook
{
    public string Name
    {
        get { return "IrcCalc"; }
    }
    public string Version
    {
        get { return "2.0a2"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"calc", @"calc <expression> - Calculates expression, accepted operators: ""+"", ""-"", ""*""," +
                    @" ""/"", ""^""."}
            };
        }
    }

    static CalcEnvironment CalcEnv = new CalcEnvironment();


    public void Stop()
    {}

    [ImportingConstructor]
    public Calc(IIrcComm irc, IMeidoComm meido)
    {
        meido.RegisterTrigger("calc", HandleTrigger);
        irc.AddQueryMessageHandler(HandleMessage);
    }


    public static void HandleTrigger(IIrcMessage e)
    {
        if (e.MessageArray.Length > 1)
        {
            var exprStr = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
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


    public static void HandleMessage(IIrcMessage e)
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


    static void OutputError<T>(IIrcMessage e, GenericExpression<T> expr)
    {
        string error = expr.ErrorMessage;
        if (expr.ErrorPosition >= 0)
            error += " | Postion: " + expr.ErrorPosition;

        e.Reply(error);
    }
}