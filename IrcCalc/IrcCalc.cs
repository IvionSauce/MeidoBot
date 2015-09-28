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
        get { return "1.13"; }
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
    public Calc(IMeidoComm meido)
    {
        meido.RegisterTrigger("calc", HandleTrigger);
    }

    public void HandleTrigger(IIrcMessage e)
    {
        if (e.MessageArray.Length > 1)
        {
            var exprStr = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);

            var tokenizer = new Tokenizer();
            var expr = tokenizer.Tokenize(exprStr);

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
}