using System;
using Meebey.SmartIrc4net;


namespace MeidoBot
{
    class Chatlogger : IChatlogger
    {
        readonly IrcClient irc;
        readonly ChatWriter writer;

        const string messageFmt = "<{0}> {1}";
        const string actionFmt = "* {0} {1}";
        const string noticeFmt = ">{0}< {1}";


        public Chatlogger(IrcClient irc, LogWriter logWriter, string chatlogDir)
        {
            this.irc = irc;
            writer = new ChatWriter(logWriter, chatlogDir);

            Subscribe();
        }

        void Subscribe()
        {
            irc.OnChannelMessage += Message;
            irc.OnQueryMessage += Message;

            irc.OnChannelAction += Action;
            irc.OnQueryAction += Action;

            irc.OnChannelNotice += Notice;
            irc.OnQueryNotice += Notice;

            irc.OnJoin += Join;
            irc.OnPart += Part;
            irc.OnKick += Kick;

            irc.OnChannelModeChange += ChannelMode;
            irc.OnTopic += Topic;
            irc.OnTopicChange += TopicChange;

            irc.OnNickChange += NickChange;
            /* OnRawMessage is raised before OnQuit (and most other events) in IrcClient.
             * This knowledge breaks abstraction, but allows us to leverage the tracking (of users and channels)
             * that SmartIrc4Net's IrcClient offers with ActiveChannelSyncing turned on.
             * When OnQuit is raised IrcClient has already removed the user from its tracking, so we break the
             * abstraction because otherwise we'd have to maintain our own tracking. While not ideal I like it better
             * than keeping a tracking structure parallel to IrcClient's. */
            irc.OnRawMessage += RawQuit;
            irc.OnDisconnected += Disconnect;
            irc.OnConnectionError += ConnectionError;
        }


        // --------------------------
        // Outgoing messages logging.
        // --------------------------

        public void Message(string target, string message)
        {
            writer.Log(target,
                messageFmt, irc.Nickname, message);
        }

        public void Action(string target, string message)
        {
            writer.Log(target,
                actionFmt, irc.Nickname, message);
        }

        public void Notice(string target, string message)
        {
            writer.Log(target,
                noticeFmt, irc.Nickname, message);
        }


        // --------------------------
        // Incoming messages logging.
        // --------------------------

        void Message(object s, IrcEventArgs e)
        {
            writer.Log(e.Data.Channel ?? e.Data.Nick,
                messageFmt, e.Data.Nick, e.Data.Message);
        }

        void Action(object s, ActionEventArgs e)
        {
            writer.Log(e.Data.Channel ?? e.Data.Nick,
                actionFmt, e.Data.Nick, e.ActionMessage);
        }


        void Notice(object s, IrcEventArgs e)
        {
            var source = e.Data.Channel ?? e.Data.Nick;
            // Ignore notices from server.
            if (source != null)
            {
                writer.Log(source,
                    noticeFmt, e.Data.Nick, e.Data.Message);
            }
        }


        // ---------------------------------------
        // IRC events related to a single channel.
        // ---------------------------------------

        void Join(object s, JoinEventArgs e)
        {
            writer.Log(e.Channel,
                "--> {0} ({1}@{2}) has joined", e.Who, e.Data.Ident, e.Data.Host);

            if (irc.IsMe(e.Who))
            {
                writer.Log(e.Channel,
                    "-!- Joined channel {0} as {1}", e.Channel, e.Who);
            }
        }

        void Part(object s, PartEventArgs e)
        {
            writer.Log(e.Channel,
                "<-- {0} ({1}@{2}) has left [{3}]",
                e.Who, e.Data.Ident, e.Data.Host, e.PartMessage);

            if (irc.IsMe(e.Who))
                writer.CloseLog(e.Channel);
        }

        void Kick(object s, KickEventArgs e)
        {
            writer.Log(e.Channel,
                "<-- {0} was kicked by {1} [{2}]", e.Whom, e.Who, e.KickReason);

            if (irc.IsMe(e.Whom))
                writer.CloseLog(e.Channel);
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
                
                writer.Log(e.Channel,
                    "-!- {0} has changed channel mode: {1} {2}", e.Data.Nick, action, m.Parameter);
            }
        }

        void Topic(object s, TopicEventArgs e)
        {
            writer.Log(e.Channel,
                "-!- Channel topic: {0}", e.Topic);
        }

        void TopicChange(object s, TopicChangeEventArgs e)
        {
            writer.Log(e.Channel,
                "-!- {0} changed the topic to: {1}", e.Who, e.NewTopic);
        }


        // ---------------------------------------------------
        // IRC events related to (possibly) multiple channels.
        // ---------------------------------------------------

        void NickChange(object s, NickChangeEventArgs e)
        {
            foreach (var channel in GetJoinedChannels(e.NewNickname))
            {
                writer.Log(channel,
                    "-!- {0} is now known as {1}", e.OldNickname, e.NewNickname);
            }

            writer.CloseLog(e.OldNickname);
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
                    writer.Log(channel,
                        "<-- {0} ({1}@{2}) has quit [{3}]",
                        e.Data.Nick, e.Data.Ident, e.Data.Host, e.Data.Message);
                }

                writer.CloseLog(e.Data.Nick);
            }
        }

        void Disconnect(object s, EventArgs e)
        {
            foreach (var channel in irc.GetChannels())
            {
                writer.Log(channel, "-!- Disconnected from {0}", irc.Address);
            }
        }

        void ConnectionError(object s, EventArgs e)
        {
            foreach (var channel in irc.GetChannels())
            {
                writer.Log(channel, "-!- Error in connection to {0}", irc.Address);
            }
        }


        // ---------------
        // Helper methods.
        // ---------------

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

                return new string[0];
            }
        }

    }
}