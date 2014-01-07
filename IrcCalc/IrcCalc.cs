using System.Collections.Generic;
using Calculation;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

[Export(typeof(IMeidoHook))]
public class Calc : IMeidoHook
{
    IIrcComm irc;

    public string Prefix { get; set; }

    public string Name
    {
        get { return "IrcCalc"; }
    }
    public string Version
    {
        get { return "1.0"; }
    }

    public Dictionary<string,string> exportedHelp
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


    [ImportingConstructor]
    public Calc(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        if (e.MessageArray[0] == ".calc" && e.MessageArray.Length > 1)
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
                irc.SendMessage(e.Channel, string.Format
                                ("{0}: I am but a simple meido, please supply a correct expression.", e.Nick));
                return;
            }

            double result = ShuntingYard.Calculate(tokenizedExpr);
            irc.SendMessage(e.Channel, string.Format("{0}: {1}", e.Nick, result));
        }
    }
}