﻿using System.Collections.Generic;
using MeidoCommon.Formatting;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcTell : IMeidoHook
{
    public string Name
    {
        get { return "Tell"; }
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
                {"tell", "tell <nick> <message> - Relay message to nick, will be relayed when nick is active. " +
                    "See also: tell-read, tell-show, tell-clear"},
                
                {"tell-read", "tell-read [number] - Read message associated with number. Reads next waiting message " +
                    "if no number was supplied."},
                
                {"tell-show", "tell-show - Shows an overview of all your waiting messages."},
                
                {"tell-clear", "tell-clear - Clears all your waiting messages."}
            };
        }
    }

    readonly IIrcComm irc;
    readonly Inboxes inboxes;

    public void Stop()
    {}

    [ImportingConstructor]
    public IrcTell(IIrcComm irc, IMeidoComm meido)
    {
        inboxes = new Inboxes(meido.DataDir);

        meido.RegisterTrigger("tell", Tell);
        meido.RegisterTrigger("tell-read", Read);
        meido.RegisterTrigger("tell-show", Show);
        meido.RegisterTrigger("tell-clear", Clear);

        irc.AddChannelMessageHandler(MessageHandler);
        this.irc = irc;
    }


    public void Tell(IIrcMessage e)
    {
        // tell <nick> <message>
        string destinationNick, message;
        if (TryGetArgs(e.MessageArray, out destinationNick, out message))
        {
            var inbox = inboxes.GetOrNew(destinationNick);

            if (inbox.Add(e.Nick, message))
            {
                e.Reply("Sending message to {0} when they're active.", destinationNick);
                inboxes.Save(inbox);
            }
            else
                e.Reply("Sorry, {0}'s inbox is full!", destinationNick);
        }
    }

    static bool TryGetArgs(string[] ircMsg, out string toNick, out string tellMsg)
    {
        if (ircMsg.Length > 2)
        {
            toNick = ircMsg[1].Trim();
            tellMsg = string.Join(" ", ircMsg, 2, ircMsg.Length - 2).Trim();

            if (toNick != string.Empty && tellMsg != string.Empty)
                return true;
        }

        toNick = null;
        tellMsg = null;
        return false;
    }


    public void Read(IIrcMessage e)
    {
        var inbox = inboxes.Get(e.Nick);
        if (inbox == null || inbox.MessagesCount == 0)
        {
            irc.SendNotice(e.Nick, "You have no messages to read.");
            return;
        }

        TellEntry tell;
        int tellIdx;
        // tell-read [number]
        if (TryGetIdx(e.MessageArray, out tellIdx))
        {
            if (inbox.TryRead(tellIdx, out tell))
            {
                irc.SendNotice(e.Nick, FormatTell(tell));
                inboxes.Save(inbox);
            }
            else
                irc.SendNotice(e.Nick, "No such message.");
        }
        // tell-read
        else if (inbox.TryReadNext(out tell))
        {
            irc.SendNotice(e.Nick, FormatTell(tell));
            inboxes.Save(inbox);
        }
    }

    static bool TryGetIdx(string[] msg, out int index)
    {
        if (msg.Length > 1)
        {
            if (int.TryParse(msg[1], out index))
                return true;
        }

        index = 0;
        return false;
    }


    public void Show(IIrcMessage e)
    {
        var inbox = inboxes.Get(e.Nick);
        if (inbox != null)
        {
            Overview(e.Nick, inbox.GetAll());
        }

        irc.SendNotice(e.Nick, " -----");
    }

    void Overview(string nick, TellEntry[] entries)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            irc.SendNotice(nick, "[{0}] From {1}, {2} ago.",
                           i, entry.From, Format.DurationWithDays(entry.ElapsedTime));
        }
    }


    public void Clear(IIrcMessage e)
    {
        var inbox = inboxes.Get(e.Nick);
        int count = 0;
        if (inbox != null)
        {
            count = inbox.MessagesCount;
            inbox.ClearMessages();
        }

        irc.SendNotice(e.Nick, "Cleared all your messages. (Count: {0})", count);
        if (count > 0)
            inboxes.Save(e.Nick);
    }


    public void MessageHandler(IIrcMessage e)
    {
        var inbox = inboxes.Get(e.Nick);
        if ( inbox != null && inbox.NewMessages )
        {
            Notify(e.Nick, inbox);
        }
    }

    void Notify(string nick, TellsInbox inbox)
    {
        TellEntry entry;
        if (inbox.TryReadNext(out entry))
        {
            irc.SendNotice(nick, FormatTell(entry));
            if (inbox.MessagesCount > 0)
                irc.SendNotice(nick, "You have {0} other message(s) waiting.", inbox.MessagesCount);

            inboxes.Save(inbox);
        }
    }


    static string FormatTell(TellEntry entry)
    {
        return string.Format("Sent by {0}, {1} ago: {2}",
                             entry.From, Format.DurationWithDays(entry.ElapsedTime),
                             entry.Message);
    }

}