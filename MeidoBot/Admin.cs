using System;
using Meebey.SmartIrc4net;
using MeidoCommon;


namespace MeidoBot
{
    class Admin
    {
        readonly IrcClient irc;
        readonly Meido bot;
        readonly MeidoComm meidoComm;


        public Admin(Meido bot, IrcClient irc, MeidoComm meidoComm)
        {
            this.bot = bot;
            this.irc = irc;
            this.meidoComm = meidoComm;
        }


        public void AdminTrigger(IIrcMessage msg)
        {
            if (meidoComm.AuthLevel(msg.Nick) >= 2)
            {
                string trigger = null;
                if (msg.MessageArray.Length > 1)
                    trigger = msg.MessageArray[1];

                // Admin triggers.
                switch (trigger)
                {
                case "j":
                case "join":
                    for (int i = 2; i < msg.MessageArray.Length; i++)
                        irc.RfcJoin(msg.MessageArray[i]);

                    return;

                case "p":
                case "part":
                    for (int i = 2; i < msg.MessageArray.Length; i++)
                        irc.RfcPart(msg.MessageArray[i]);
                    
                    return;

                case "nick":
                    if (msg.MessageArray.Length == 3)
                    {
                        msg.Reply("Attempting to change nick from {0} to {1}.", irc.Nickname, msg.MessageArray[2]);
                        irc.RfcNick(msg.MessageArray[2]);
                    }
                    else
                        msg.Reply("Current nick is {0}.", irc.Nickname);

                    return;

                case "channels":
                    var channels = string.Join(", ", irc.GetChannels());
                    msg.Reply(channels);
                    return;

                case "servers":
                    var servers = string.Join(", ", MeidoManager.GetServers());
                    msg.Reply(servers);
                    return;
                }
                // Owner only triggers.
                if (meidoComm.AuthLevel(msg.Nick) == 3)
                {
                    switch (trigger)
                    {
                    case "dc":
                    case "disconnect":
                        msg.Reply("Disconnecting from {0}.", irc.Address);
                        MeidoManager.StopBot(irc.Address);
                        return;

                    case "dc-all":
                    case "disconnect-all":
                        msg.Reply("Disconnecting from all servers.");
                        MeidoManager.StopAllBots();
                        return;

                    case "restart":
                        msg.Reply("Restarting bot for {0}.", irc.Address);
                        MeidoManager.RestartBot(irc.Address);
                        return;

                    case "restart-all":
                        msg.Reply("Restarting bots for all servers.");
                        MeidoManager.RestartAllBots();
                        return;

                    case "reload":
                        msg.Reply("Reloading plugins...");
                        bot.ReloadPlugins();
                        msg.Reply("Done reloading plugins.");
                        return;
                    }
                }
            }
            else
                msg.Reply("Authentication Level insufficient. Either you're not an admin or you need to authenticate.");
        }


        public void AuthTrigger(IIrcMessage msg)
        {
            if (msg.MessageArray.Length > 1)
            {
                if (meidoComm.Auth(msg.Nick, msg.MessageArray[1]))
                    msg.Reply("You've successfully authenticated.");
            }

            msg.Reply("Your current Authentication Level is " + meidoComm.AuthLevel(msg.Nick));
        }

    }
}