using System.Collections.Generic;
using Calculation;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

[Export(typeof(IMeidoHook))]
public class Calc : IMeidoHook
{
    readonly IIrcComm irc;

    public string Prefix { get; set; }

    public string Name
    {
        get { return "IrcCalc"; }
    }
    public string Version
    {
        get { return "1.1"; }
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
    public Calc(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        if (e.MessageArray[0] == Prefix + "calc" && e.MessageArray.Length > 1)
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
                                ("{0}: I am but a simple meido, please ask me something I understand.", e.Nick));
                return;
            }

            double result = ShuntingYard.Calculate(tokenizedExpr);
            irc.SendMessage( e.Channel, string.Format("{0}: {1}", e.Nick, result) );
        }
    }
}