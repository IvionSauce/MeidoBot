using System;
using System.Collections.Generic;
using System.Threading;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class MiscUtils : IMeidoHook
{
    IIrcComm irc;

    List<Timer> timers = new List<Timer>();

    public string Prefix { get; set; }

    public string Name
    {
        get { return "MiscUtils"; }
    }
    public string Version
    {
        get { return "0.35"; }
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
    {
        foreach (Timer tmr in timers)
            tmr.Dispose();
    }

    [ImportingConstructor]
    public MiscUtils(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
        irc.AddQueryMessageHandler(HandlePM);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        // Say trigger.
        if (e.MessageArray[0] == Prefix + "say" &&
            e.MessageArray.Length > 1)
        {
            string toChannel = Say(e.MessageArray, e.Channel);
            Console.WriteLine("\n--- Say: {0}/{1} -> {2}", e.Channel, e.Nick, toChannel);
        }

        // Timer trigger.
        else if (e.MessageArray[0] == Prefix + "timer" &&
                 e.MessageArray.Length > 1)
        {
            double minutes;
            if (double.TryParse(e.MessageArray[1], out minutes))
            {
                if (minutes <= 0)
                    return;

                string[] info = {e.Channel, e.Nick};
                var time = TimeSpan.FromMinutes(minutes);

                timers.Add( new Timer(IrcTimer, info, time, TimeSpan.Zero) );
                irc.SendMessage( e.Channel, string.Format("{0}: Your timer has started.", e.Nick) );
            }
        }
    }

    public void HandlePM(IIrcMessage e)
    {
        if (e.MessageArray[0] == "say")
        {
            if (e.MessageArray.Length > 2)
            {
                string toChannel = Say(e.MessageArray, "");
                Console.WriteLine("\n--- Say: PM/{1} -> {2}", e.Channel, e.Nick, toChannel);
            }
        }
    }

    void IrcTimer(object data)
    {
        var info = (string[])data;
        irc.SendMessage( info[0], string.Format("{0}: !RINGRING! Your timer has finished.", info[1]) );
    }

    string Say(string[] command, string currentChannel)
    {
        string channel, message;
        if (command[1].StartsWith("#"))
        {
            channel = command[1];
            message = string.Join(" ", command, 2, command.Length - 2);
        }
        else
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
        return null;
    }

    bool InChannel(string channel)
    {
        foreach (string joinedChannel in irc.GetChannels())
        {
            if (channel.ToLower() == joinedChannel.ToLower())
                return true;
        }
        return false;
    }
}