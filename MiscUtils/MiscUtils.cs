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
                {"timer", "timer <minutes> - Starts a timer. Minutes must be positive."}
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
        double minutes;
        if ( e.MessageArray.Length == 2 && double.TryParse(e.MessageArray[1], out minutes) )
        {
            if (minutes <= 0)
                return;
            var time = TimeSpan.FromMinutes(minutes);
            
            ThreadPool.QueueUserWorkItem( (state) => IrcTimer(e, time) );
            e.Reply("Your timer has started.");
        }
    }

    void IrcTimer(IIrcMessage e, TimeSpan duration)
    {
        Thread.Sleep(duration);
        e.Reply("!RINGRING! Your timer has finished.");
    }


    string Say(string[] command, string currentChannel)
    {
        string channel = null;
        string message = null;
        // say #channel message
        // Send message to specified channel.
        if ( command.Length > 2 && command[1].StartsWith("#") )
        {
            channel = command[1];
            message = string.Join(" ", command, 2, command.Length - 2);
        }
        // say message
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