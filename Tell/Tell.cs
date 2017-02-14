using System.Collections.Generic;
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
                    "See also: tell-read, tell-clear"},
                
                {"tell-read", "tell-read [n] - Read the next `n` messages, defaults to 5."},
                
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
        // Short circuit tell to tell-read if not enough arguments.
        else
            Read(e);
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
        // tell-read [amount]
        var inbox = inboxes.Get(e.Nick);
        var tells = inbox.Read(GetAmount(e.MessageArray));
        if (tells.Length > 0)
        {
            foreach (TellEntry tellMsg in tells)
                irc.SendNotice(e.Nick, FormatTell(tellMsg));

            irc.SendNotice(e.Nick, "You have {0} more message(s) waiting.", inbox.MessagesCount);
            inboxes.Save(inbox);
        }
        else
            irc.SendNotice(e.Nick, "No messages to read.");
    }

    static int GetAmount(string[] msg)
    {
        const int stdAmount = 5;

        if (msg.Length > 1)
        {
            int amount;
            if (int.TryParse(msg[1], out amount))
                return amount;
        }

        return stdAmount;
    }


    public void Clear(IIrcMessage e)
    {
        var inbox = inboxes.Get(e.Nick);
        int count = inbox.MessagesCount;

        inbox.ClearMessages();

        irc.SendNotice(e.Nick, "Cleared all your messages. (Count: {0})", count);
        if (count > 0)
            inboxes.Save(e.Nick);
    }


    public void MessageHandler(IIrcMessage e)
    {
        var inbox = inboxes.Get(e.Nick);
        if (inbox.NewMessages)
        {
            irc.SendNotice(e.Nick, "You have {0} tell message(s) waiting.", inbox.MessagesCount);
            inbox.NewMessages = false;
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