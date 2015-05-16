using System.Collections.Generic;
using Calculation;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

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
            var expr = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
            string [] tokenizedExpr;

            var tokenizer = new Tokenizer();
            try
            {
                tokenizedExpr = tokenizer.Tokenize(expr);
            }
            catch (MalformedExpressionException)
            {
                e.Reply("I am but a simple meido, please ask me something I can understand.");
                return;
            }

            double result = ShuntingYard.Calculate(tokenizedExpr);
            e.Reply( result.ToString() );
        }
    }
}