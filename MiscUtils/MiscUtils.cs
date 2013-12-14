using System;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class MiscUtils : IMeidoHook
{
    IIrcComm irc;

    public string Description
    {
        get { return "MiscUtils v0.28"; }
    }

    public Dictionary<string,string> exportedHelp
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {".tell",
                    ".tell <channel> <message> - If bot is in the specified channel, send message to the channel."},
                {".say",
                    ".say <channel> <message> - If bot is in the specified channel, send message to the channel."}
            };
        }
    }


    [ImportingConstructor]
    public MiscUtils(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        switch (e.MessageArray[0])
        {
        case ".say":
        case ".tell":
            if (e.MessageArray.Length > 2)
            {
                Console.WriteLine("\n--- Tell: {0}/{1} -> {2}", e.Channel, e.Nick, e.MessageArray[1]);
                Tell(e.MessageArray);
            }
            break;
        }
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

    void Tell(string[] command)
    {
        // Index 0 is ".tell"/".say", index 1 is `channel` and everything from index 2 onward is the `message`.
        string channel = command[1];
        var message = string.Join(" ", command, 2, command.Length - 2);

        if (InChannel(channel))
            irc.SendMessage(channel, message);
    }
}