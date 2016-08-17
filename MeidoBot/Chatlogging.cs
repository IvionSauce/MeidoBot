using System;
using System.Threading;
using Meebey.SmartIrc4net;


namespace MeidoBot
{
    class Chatlogger
    {
        readonly IrcClient irc;

        const string messageFmt = "<{0}> {1}";
        const string actionFmt = "* {0} {1}";
        const string noticeFmt = ">{0}< {1}";


        public Chatlogger(IrcClient irc)
        {
            irc.OnChannelMessage += Message;
            irc.OnQueryMessage += Message;

            irc.OnChannelAction += Action;
            irc.OnQueryAction += Action;

            irc.OnChannelNotice += Notice;
            irc.OnQueryNotice += Notice;
            irc.OnCtcpRequest += Ctcp;

            irc.OnJoin += Join;
            irc.OnPart += Part;
            irc.OnKick += Kick;

            irc.OnNickChange += NickChange;

            /* OnRawMessage is raised before OnQuit (and most other events) in IrcClient.
             * This knowledge breaks abstraction, but allows us to leverage the tracking (of users and channels)
             * that SmartIrc4Net's IrcClient offers with ActiveChannelSyncing turned on.
             * When OnQuit is raised IrcClient has already removed the user from its tracking, so we break the
             * abstraction because otherwise we'd have to maintain our own tracking. While not ideal I like it better
             * than keeping a tracking structure parallel to IrcClient's. */
            irc.OnRawMessage += RawQuit;

            irc.OnChannelModeChange += ChannelMode;

            irc.OnTopic += Topic;
            irc.OnTopicChange += TopicChange;

            this.irc = irc;
        }


        public void Message(string target, string message)
        {
            Log(target,
                messageFmt, irc.Nickname, message);
        }

        public void Action(string target, string message)
        {
            Log(target,
                actionFmt, irc.Nickname, message);
        }

        public void Notice(string target, string message)
        {
            Log(target,
                noticeFmt, irc.Nickname, message);
        }


        void Message(object s, IrcEventArgs e)
        {
            Log(e.Data.Channel ?? e.Data.Nick,
                messageFmt, e.Data.Nick, e.Data.Message);
        }

        void Action(object s, ActionEventArgs e)
        {
            Log(e.Data.Channel ?? e.Data.Nick,
                actionFmt, e.Data.Nick, e.ActionMessage);
        }


        void Notice(object s, IrcEventArgs e)
        {
            var source = e.Data.Channel ?? e.Data.Nick;
            // Ignore notices from server.
            if (source != null)
            {
                Log(source,
                    noticeFmt, e.Data.Nick, e.Data.Message);
            }
        }

        void Ctcp(object s, CtcpEventArgs e)
        {
            var ctcpCmd = e.CtcpCommand;
            if (!string.IsNullOrEmpty(e.CtcpParameter))
                ctcpCmd += e.CtcpParameter;
            
            Log(e.Data.Nick,
                "-!- Received a CTCP {0} from {1}",
                ctcpCmd, e.Data.Nick);
        }


        void Join(object s, JoinEventArgs e)
        {
            Log(e.Channel,
                "--> {0} ({1}@{2}) has joined", e.Who, e.Data.Ident, e.Data.Host);
            
            if (irc.IsMe(e.Who))
            {
                Log(e.Channel,
                    "-!- Joined channel {0} as {1} on {2:ddd dd MMM yyyy}",
                    e.Channel, e.Who, DateTime.Now);
            }
        }

        void Part(object s, PartEventArgs e)
        {
            Log(e.Channel,
                "<-- {0} ({1}@{2}) has left [{3}]",
                e.Who, e.Data.Ident, e.Data.Host, e.PartMessage);
        }

        void Kick(object s, KickEventArgs e)
        {
            Log(e.Channel,
                "<-- {0} was kicked by {1} [{2}]", e.Whom, e.Who, e.KickReason);
        }


        void NickChange(object s, NickChangeEventArgs e)
        {
            foreach (var channel in GetJoinedChannels(e.NewNickname))
            {
                Log(channel,
                    "-!- {0} is now known as {1}", e.OldNickname, e.NewNickname);
            }
        }


        void RawQuit(object s, IrcEventArgs e)
        {
            // Because we hook in before the extra parsing the more specific events offer, we have to do
            // a little bit ourselves. Luckily raw quit messages are simple.
            if (e.Data.Type == ReceiveType.Quit)
            {
                // But because we hook in before OnQuit we have access to the JoinedChannels property (via IrcUser),
                // which allows us to log to the appropriate channel(s).
                foreach (var channel in GetJoinedChannels(e.Data.Nick))
                {
                    Log(channel,
                        "<-- {0} ({1}@{2}) has quit [{3}]",
                        e.Data.Nick, e.Data.Ident, e.Data.Host, e.Data.Message);
                }
            }
        }


        void ChannelMode(object s, ChannelModeChangeEventArgs e)
        {
            foreach (var m in e.ModeChanges)
            {
                string action;
                if (m.Action == ChannelModeChangeAction.Set)
                    action = "+" + m.ModeChar;
                else
                    action = "-" + m.ModeChar;
                
                Log(e.Channel,
                    "-!- {0} has changed channel mode: {1} {2}", e.Data.Nick, action, m.Parameter);
            }
        }


        void Topic(object s, TopicEventArgs e)
        {
            Log(e.Channel,
                "-!- Channel topic: {0}", e.Topic);
        }

        void TopicChange(object s, TopicChangeEventArgs e)
        {
            Log(e.Channel,
                "-!- {0} changed the topic to: {1}", e.Who, e.NewTopic);
        }


        string[] GetJoinedChannels(string nick)
        {
            // Leverage IrcClient's internal tracking.
            var user = irc.GetIrcUser(nick);
            if (user != null)
                return user.JoinedChannels;
            else
            {
                /* Should only be null if:
                 * 1. user is not in any channels we're in
                 * 2. IrcClient has not yet synced channel+users
                 * 3. user has quit (which also satisfies the first condition)
                 * 
                 * The first is a non-issue. The second is regrettable but unavoidable. The third is annoying, since
                 * we want to know the channels they were part of for logging purposes. (which is solved by `RawQuit`)
                */

                Console.WriteLine("--- User was null for " + nick);
                return new string[0];
            }
        }


        void Log(string source, string logMsg, params object[] args)
        {
            Log(source, string.Format(logMsg, args));
        }

        void Log(string source, string logMsg)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            Console.WriteLine("   [{0}] {1}/{2}", timestamp, source, logMsg);
        }
    }
}