using System;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class MiscUtils : IMeidoHook, IPluginTriggers
{
    public string Name
    {
        get { return "MiscUtils"; }
    }
    public string Version
    {
        get { return "0.54"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    readonly IIrcComm irc;
    readonly ILog log;

    readonly TimerTrigger timerTrig;


    public void Stop()
    {}

    [ImportingConstructor]
    public MiscUtils(IIrcComm ircComm, IMeidoComm meido)
    {
        irc = ircComm;
        log = meido.CreateLogger(this);
        timerTrig = new TimerTrigger();

        var timerHelp = new TriggerHelp(
            "<duration> [message]",
            "Starts a timer. Duration is either in the form of '1h45m30s' (where each part, h/m/s, is optional) or " +
            "just a number, which is taken to be minutes. If called with no arguments this'll give an overview of " +
            "all your running timers.",

            new CommandHelp(
                "stop", "[index]",
                "Stops previously started timer. If called without an index all timers are stopped."),
            new CommandHelp(
                "change", "<index> <delta>",
                "Change previously started timer by `delta`. Delta is either in the form of '1h45m30s' or just " +
                "minutes as a bare number. Delta can be prefixed with either + or -, to substract or add to " +
                "the timer (in fact, the shorthand requires you do so).\nShorthand: timer <delta> [index]")
        );

        Triggers = new Trigger[] {
            new Trigger("timer", timerTrig.Timer) { Help = timerHelp },

            new Trigger("say", Say) {
                Help = new TriggerHelp(
                    "[channel] <message>",
                    "If bot is in the specified channel, send message to the channel. If no channel is given, " +
                    "message will be sent to current channel.")
            }
        };
    }


    void Say(ITriggerMsg e)
    {
        string channel = null;
        string message = null;
        // say [channel] <message>
        // Send message to specified channel.
        if ( e.MessageArray.Length > 2 && e.MessageArray[1].StartsWith("#", StringComparison.Ordinal) )
        {
            channel = e.MessageArray[1];
            message = string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2);
        }
        // say <message>
        // Send message to current channel.
        else if (e.MessageArray.Length > 1)
        {
            channel = e.Channel;
            message = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
        }

        if ( Say(channel, message) )
        {
            string origin = e.Channel ?? "PM";
            log.Message("Say: {0}/{1} -> {2}", origin, e.Nick, channel);
        }
    }

    bool Say(string channel, string message)
    {
        if (!string.IsNullOrEmpty(channel) && InChannel(channel))
        {
            irc.SendMessage(channel, message);
            return true;
        }
        else
            return false;
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