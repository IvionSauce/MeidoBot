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
        get { return "1.15"; }
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


    public void Stop()
    {}

    [ImportingConstructor]
    public Calc(IIrcComm irc, IMeidoComm meido)
    {
        meido.RegisterTrigger("calc", HandleTrigger);
        irc.AddChannelMessageHandler(HandleMessage);
    }


    public void HandleTrigger(IIrcMessage e)
    {
        if (e.MessageArray.Length > 1)
        {
            var exprStr = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
            var expr = TokenizedExpression.Parse(exprStr);

            if (expr.Success)
            {
                double result = ShuntingYard.Calculate(expr);
                e.Reply( result.ToString() );
            }
            else
            {
                string error = expr.ErrorMessage;
                if (expr.ErrorPosition >= 0)
                    error += " | Postion: " + expr.ErrorPosition;
                
                e.Reply(error);
            }
        }
    }


    public void HandleMessage(IIrcMessage e)
    {
        if (e.Trigger == null)
        {
            var expr = TokenizedExpression.Parse(e.Message);
            if (expr.Success)
            {
                double result = ShuntingYard.Calculate(expr);
                e.Reply( result.ToString() );
            }
        }
    }
}