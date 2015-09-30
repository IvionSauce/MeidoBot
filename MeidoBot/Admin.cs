using System;
using Meebey.SmartIrc4net;
using MeidoCommon;


namespace MeidoBot
{
    class Admin
    {
        readonly IrcClient irc;
        readonly MeidoComm meido;


        public Admin(IrcClient irc, MeidoComm meido)
        {
            this.irc = irc;
            this.meido = meido;
        }


        public void AdminTrigger(IIrcMessage msg)
        {
            if (meido.AuthLevel(msg.Nick) >= 2)
            {
                string trigger = null;
                if (msg.MessageArray.Length > 1)
                    trigger = msg.MessageArray[1];

                // Admin triggers.
                switch (trigger)
                {
                case "join":
                    for (int i = 2; i < msg.MessageArray.Length; i++)
                        irc.RfcJoin(msg.MessageArray[i]);

                    return;

                case "part":
                    for (int i = 2; i < msg.MessageArray.Length; i++)
                        irc.RfcPart(msg.MessageArray[i]);
                    
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
                if (meido.AuthLevel(msg.Nick) == 3)
                {
                    switch (trigger)
                    {
                    case "disconnect":
                        msg.Reply("Disconnecting from {0}.", irc.Address);
                        MeidoManager.StopBot(irc.Address);
                        return;

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
                if (meido.Auth(msg.Nick, msg.MessageArray[1]))
                    msg.Reply("You've successfully authenticated.");
            }

            msg.Reply("Your current Authentication Level is " + meido.AuthLevel(msg.Nick));
        }

    }
}