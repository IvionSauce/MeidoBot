using System.Collections.Generic;
using MeidoCommon.Formatting;
using MeidoCommon.Parsing;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcTell : IMeidoHook, IPluginTriggers, IPluginIrcHandlers
{
    public string Name
    {
        get { return "Tell"; }
    }
    public string Version
    {
        get { return "0.16"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }
    public IEnumerable<IIrcHandler> IrcHandlers { get; private set; }


    readonly IIrcComm irc;
    readonly Inboxes inboxes;

    public void Stop()
    {}

    [ImportingConstructor]
    public IrcTell(IIrcComm irc, IMeidoComm meido)
    {
        var threading = TriggerThreading.Queue;
        Triggers = Trigger.Group(
            
            new Trigger("tell", Tell, threading) {
                Help = new TriggerHelp(
                    "[<nick> <message>] | [count]",
                    "Store message for nick, will be relayed when nick is active. If called with 1 or 0 arguments " +
                    "this will read your tells, up to `count`.")
            },

            new Trigger(Read, threading, "tell-read", "tells") {
                Help = new TriggerHelp(
                    "[count]",
                    "Read stored tell messages, up to `count` (defaults to 5).")
            },

            new Trigger("tell-clear", Clear, threading) {
                Help = new TriggerHelp(
                    "Clears all your stored tell messages.")
            }
        );

        IrcHandlers = new IIrcHandler[] {
            new IrcHandler<IChannelMsg>(MessageHandler, threading)
        };

        this.irc = irc;
        inboxes = new Inboxes(meido.DataDir);
    }


    public void Tell(ITriggerMsg e)
    {
        // tell <nick> <message>
        var message =
            e.GetArg(out string destinationNick)
            .ToJoined(JoinedOptions.TrimExterior);

        if (ParseArgs.Success(destinationNick, message))
        {
            var inbox = inboxes.GetOrNew(destinationNick);

            if (inbox.Add(e.Nick, message))
            {
                e.Reply("Sending message to {0} when they're active. [{1}/{2}]",
                        destinationNick, inbox.MessagesCount, inbox.Capacity);
                
                inboxes.Save(inbox);
            }
            else
                e.Reply("Sorry, {0}'s inbox is full!", destinationNick);
        }
        // Short circuit tell to tell-read if not enough arguments.
        else
            Read(e);
    }


    public void Read(ITriggerMsg e)
    {
        // tell-read [amount]
        var inbox = inboxes.Get(e.Nick);
        var tells = inbox.Read(Amount(e.GetArg()));
        if (tells.Length > 0)
        {
            foreach (TellEntry tellMsg in tells)
                irc.SendNotice(e.Nick, FormatTell(tellMsg));

            irc.SendNotice(e.Nick, " ----- {0} message(s) remaining.", inbox.MessagesCount);
            inboxes.Save(inbox);
        }
        else
            irc.SendNotice(e.Nick, "No messages to read.");
    }

    static int Amount(string amountArg)
    {
        const int stdAmount = 5;

        if (int.TryParse(amountArg, out int amount) && amount > 0)
        {
            return amount;
        }
        return stdAmount;
    }


    public void Clear(ITriggerMsg e)
    {
        var inbox = inboxes.Get(e.Nick);
        int count = inbox.MessagesCount;

        inbox.ClearMessages();

        irc.SendNotice(e.Nick, "Cleared all your messages. (Count: {0})", count);
        if (count > 0)
            inboxes.Save(e.Nick);
    }


    public void MessageHandler(IChannelMsg e)
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