using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class MyClass : IMeidoHook
{
    IIrcComm irc;

    public string Prefix { get; set; }

    public string Name
    {
        get { return "MyClass"; }
    }
    public string Version
    {
        get { return "0.10"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {".trigger", ".trigger does x"}
            };
        }
    }

    [ImportingConstructor]
    public MyClass(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage ircMessage)
    {
        // Do something
    }
}
