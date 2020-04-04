using System;
using System.Collections.Generic;
using Meebey.SmartIrc4net;
using MeidoCommon;
using MeidoCommon.Parsing;


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


        public void AdminTrigger(ITriggerMsg msg)
        {
            if (meidoComm.AuthLevel(msg.Nick) >= 2)
            {
                string trigger = msg.GetArg(out List<string> argv);

                // Admin triggers.
                switch (trigger)
                {
                    case "j":
                    case "join":
                    foreach (var chan in argv)
                        irc.RfcJoin(chan);

                    return;

                    case "p":
                    case "part":
                    foreach (var chan in argv)
                        irc.RfcPart(chan);
                    
                    return;

                    case "nick":
                    var newNick = argv.GetArg();
                    if (newNick.HasValue())
                    {
                        msg.Reply("Attempting to change nick from {0} to {1}.", irc.Nickname, newNick);
                        irc.RfcNick(newNick);
                    }
                    else
                        msg.Reply("Current nick is {0}.", irc.Nickname);

                    return;

                    case "ch":
                    case "channels":
                    var channels = string.Join(" ", irc.GetChannels());
                    msg.Reply(channels);
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
                        bot.Dispose();
                        return;

                        case "restart":
                        Program.RestartMeido();
                        return;

                        case "gc-collect":
                        long before = GC.GetTotalMemory(false);
                        GC.Collect();
                        msg.Reply("Garbage Collection Meido: {0:N0} -> {1:N0}", before, GC.GetTotalMemory(true));
                        return;
                    }
                }
            }
            else
                msg.Reply("Authentication Level insufficient. Either you're not an admin or you need to authenticate.");
        }


        public void AuthTrigger(ITriggerMsg msg)
        {
            var passwd = msg.ArgString();
            if (passwd.HasValue() && meidoComm.Auth(msg.Nick, passwd))
            {
                msg.Reply("You've successfully authenticated.");
            }

            msg.Reply("Your current Authentication Level is " + meidoComm.AuthLevel(msg.Nick));
        }

    }
}