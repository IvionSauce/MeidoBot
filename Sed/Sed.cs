﻿using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcSed : IMeidoHook, IPluginIrcHandlers
{
    public string Name
    {
        get { return "Sed"; }
    }
    public string Version
    {
        get { return "0.11"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"sed", "Interprets messages in the form of s/<SEARCH>/<REPLACE>/<FLAGS>. For more information on " +
                    "the specifics of the search and replace expressions, see: " +
                    "https://msdn.microsoft.com/en-us/library/az24scfc.aspx"}
            };
        }
    }

    public IEnumerable<Trigger> Triggers
    {
        get { return new Trigger[0]; }
    }
    public IEnumerable<IIrcHandler> IrcHandlers { get; private set; }


    readonly IIrcComm irc;
    readonly ReplaceHistory history = new ReplaceHistory();


    public void Stop()
    {}

    [ImportingConstructor]
    public IrcSed (IIrcComm irc, IMeidoComm meido)
    {
        this.irc = irc;
        IrcHandlers = new IIrcHandler[] {
            new IrcHandler<IChannelMsg>(HandleMessage)
        };
    }


    public void HandleMessage(IChannelMsg e)
    {
        var replace = new ReplaceAction(e.Message);
        if (replace.ParseSuccess)
            Replace(replace, e.Channel, e.Nick);
        else
            history.AddMessage(e.Channel, e.Nick, e.Message);
    }

    void Replace(ReplaceAction replace, string channel, string invoker)
    {
        foreach (var item in history.GetMessages(channel))
        {
            string replacedMsg;
            switch (replace.TryReplace(item.Message, out replacedMsg))
            {
                case ReplaceResult.NoMatch:
                continue;

                case ReplaceResult.RegexTimeout:
                irc.SendNotice(invoker, "Your regular expression timed out.");
                return;

                case ReplaceResult.Success:
                if (FoulPlay(replacedMsg))
                    irc.SendNotice(invoker, "Replaced message was too long, not outputting to prevent spam.");
                else
                    irc.SendMessage(channel, "<{0}> {1}", item.Nick, replacedMsg);
                return;
            } // switch
        } // foreach
    }

    static bool FoulPlay(string message)
    {
        const int maxMsgLength = 256;

        return message.Length > maxMsgLength;
    }

}