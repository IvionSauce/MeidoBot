using System;
using System.Collections.Generic;
using System.Threading;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class MiscUtils : IMeidoHook
{
    readonly IIrcComm irc;
    readonly ILog log;

    readonly IrcTimers ircTimers = new IrcTimers();

    public string Prefix { get; set; }

    public string Name
    {
        get { return "MiscUtils"; }
    }
    public string Version
    {
        get { return "0.40"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"say", "say [channel] <message> - If bot is in the specified channel, send message to the channel. " +
                    "If no channel is given, message will be sent to current channel."},
                {"timer", "timer <duration> [message] - Starts a timer. Duration is in minutes."},
                {"timer stop", "timer stop [index] - Stops previously started timer, if no number specified will stop" +
                    "all previously started timers."},
                {"timer change", "timer change <index> <delta> - Change previously started timer by delta. Delta is" +
                    "in minutes. Shorthand: timer <delta> [index]"}
            };
        }
    }


    public void Stop()
    {}

    [ImportingConstructor]
    public MiscUtils(IIrcComm ircComm, IMeidoComm meido)
    {
        irc = ircComm;
        log = meido.CreateLogger(this);
        irc.AddTriggerHandler(HandleTrigger);
    }

    public void HandleTrigger(IIrcMessage e)
    {
        switch(e.Trigger)
        {
        case "say":
            string fromChannel = e.Channel ?? "PM";
            string toChannel = Say(e.MessageArray, e.Channel);
            if (toChannel != null)
            {
                log.Message("Say: {0}/{1} -> {2}", fromChannel, e.Nick, toChannel);
            }
            return;
        case "timer":
            Timer(e);
            return;
        }
    }


    void Timer(IIrcMessage e)
    {
        var timers = ircTimers.GetTimers(e.Nick);

        // timer
        if (e.MessageArray.Length == 1)
        {
            EmitDescriptions(timers.Descriptions(), e);
            return;
        }

        // timer <delta> [index]
        // Delta is +N or -N
        if (e.MessageArray[1].Length > 1)
        {
            char deltaSign = e.MessageArray[1][0];
            if (deltaSign == '+' || deltaSign == '-')
            {
                var delta = IrcTimers.Parse(e.MessageArray[1]);
                int index;
                if (e.MessageArray.Length >= 3)
                    index = IrcTimers.Parse(e.MessageArray[2], -1);
                else
                    index = 0;

                TimerChange(index, delta, e, timers);
                return;
            }
        }

        // timer stop [index]
        if (e.MessageArray[1] == "stop")
            TimerStop(e, timers);
        // timer change <index> <delta>
        else if (e.MessageArray.Length == 4 && e.MessageArray[1] == "change")
        {
            var delta = IrcTimers.Parse(e.MessageArray[3]);
            int index = IrcTimers.Parse(e.MessageArray[2], -1);

            TimerChange(index, delta, e, timers);
        }

        // timer <duration> [message]
        else
            TimerStart(e, timers);
    }

    static void TimerStart(IIrcMessage e, Timers timers)
    {
        // Return if invalid or negative duration.
        var duration = IrcTimers.Parse(e.MessageArray[1]);
        if (duration <= TimeSpan.Zero)
            return;

        // Grab message if one is given.
        string message = null;
        if (e.MessageArray.Length > 2)
            message = string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2);

        int tmrNo = timers.Enqueue(duration, e, message);
        if (tmrNo >= 0)
            e.Reply("Your timer has started. [{0}]", tmrNo);
        else
            e.Reply("Max timer count reached. Please wait for some timers to finish or stop them manually.");
    }

    static void TimerChange(int index, TimeSpan delta, IIrcMessage e, Timers timers)
    {
        // Return if invalid or 0 delta.
        if (delta == TimeSpan.Zero)
            return;

        TimerDescription desc;
        if (timers.Change(index, delta, out desc))
        {
            if (string.IsNullOrEmpty(desc.Message))
                e.Reply("Changed timer {0} to {1} ({2}).",
                    index, desc.Duration.Str(), desc.Remaining.Str());
            else
                e.Reply("Changed \"{0}\" to {1} ({2}).",
                    desc.Message, desc.Duration.Str(), desc.Remaining.Str());
        }
        else
            e.Reply("No such timer.");
    }

    static void TimerStop(IIrcMessage e, Timers timers)
    {
        // Stop all timers.
        if (e.MessageArray.Length == 2)
        {
            timers.StopAll();
            e.Reply("Stopped all your timers.");
        }
        // Stop one timer.
        else
        {
            int index = IrcTimers.Parse(e.MessageArray[2], -1);
            TimerDescription desc;
            if (timers.Stop(index, out desc))
            {
                if (string.IsNullOrEmpty(desc.Message))
                    e.Reply("Stopped timer {0} :: {1}/{2}",
                        index, desc.Elapsed.Str(), desc.Duration.Str());
                else
                    e.Reply("Stopped \"{0}\" :: {1}/{2}",
                        desc.Message, desc.Elapsed.Str(), desc.Duration.Str());
            }
            else
                e.Reply("No such timer.");
        }
    }


    static void EmitDescriptions(TimerDescription[] descs, IIrcMessage msg)
    {
        foreach (var desc in descs)
            EmitDescription(desc, msg);
        msg.SendNotice(" -----");
    }

    static void EmitDescription(TimerDescription desc, IIrcMessage msg)
    {
        msg.SendNotice("[{0}] {1} :: {2} ({3})",
            desc.Index, desc.Message, desc.Duration.Str(), desc.Remaining.Str());
    }


    string Say(string[] command, string currentChannel)
    {
        string channel = null;
        string message = null;
        // say [channel] <message>
        // Send message to specified channel.
        if ( command.Length > 2 && command[1].StartsWith("#") )
        {
            channel = command[1];
            message = string.Join(" ", command, 2, command.Length - 2);
        }
        // Send message to current channel.
        else if (command.Length > 1)
        {
            channel = currentChannel;
            message = string.Join(" ", command, 1, command.Length - 1);
        }

        return Say(channel, message);
    }

    string Say(string channel, string message)
    {
        if (!string.IsNullOrEmpty(channel) && InChannel(channel))
        {
            irc.SendMessage(channel, message);
            return channel;
        }
        else
            return null;
    }


    bool InChannel(string channel)
    {
        foreach (string joinedChannel in irc.GetChannels())
        {
            if ( channel.Equals(joinedChannel, StringComparison.OrdinalIgnoreCase) )
                return true;
        }
        return false;
    }
}