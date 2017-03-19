using System;
using System.Collections.Generic;
using Meebey.SmartIrc4net;


namespace MeidoBot
{
    class Chatlogger : IChatlogger
    {
        readonly IrcClient irc;
        readonly LogWriter logWriter;

        readonly string chatlogDir;
        readonly Dictionary<string, ChatlogMetaData> chatlogs =
            new Dictionary<string, ChatlogMetaData>(StringComparer.OrdinalIgnoreCase);

        int logCount;

        const string messageFmt = "<{0}> {1}";
        const string actionFmt = "* {0} {1}";
        const string noticeFmt = ">{0}< {1}";


        public Chatlogger(IrcClient irc, LogWriter writer, string chatlogDir)
        {
            this.irc = irc;
            logWriter = writer;
            this.chatlogDir = chatlogDir;

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
        }


        // --------------------------
        // Outgoing messages logging.
        // --------------------------

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


        // --------------------------
        // Incoming messages logging.
        // --------------------------

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


        // ---------------------------------------
        // IRC events related to a single channel.
        // ---------------------------------------

        void Join(object s, JoinEventArgs e)
        {
            Log(e.Channel,
                "--> {0} ({1}@{2}) has joined", e.Who, e.Data.Ident, e.Data.Host);

            if (irc.IsMe(e.Who))
            {
                Log(e.Channel,
                    "-!- Joined channel {0} as {1}", e.Channel, e.Who);
            }
        }

        void Part(object s, PartEventArgs e)
        {
            Log(e.Channel,
                "<-- {0} ({1}@{2}) has left [{3}]",
                e.Who, e.Data.Ident, e.Data.Host, e.PartMessage);

            if (irc.IsMe(e.Who))
                CloseLog(e.Channel);
        }

        void Kick(object s, KickEventArgs e)
        {
            Log(e.Channel,
                "<-- {0} was kicked by {1} [{2}]", e.Whom, e.Who, e.KickReason);

            if (irc.IsMe(e.Whom))
                CloseLog(e.Channel);
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


        // ---------------------------------------------------
        // IRC events related to (possibly) multiple channels.
        // ---------------------------------------------------

        void NickChange(object s, NickChangeEventArgs e)
        {
            foreach (var channel in GetJoinedChannels(e.NewNickname))
            {
                Log(channel,
                    "-!- {0} is now known as {1}", e.OldNickname, e.NewNickname);
            }

            CloseLog(e.OldNickname);
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

                CloseLog(e.Data.Nick);
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


        // ----------------
        // Logging methods.
        // ----------------

        void Log(string ircEntity, string logMsg, params object[] args)
        {
            ChatlogMetaData metaData;
            if (!chatlogs.TryGetValue(ircEntity, out metaData))
            {
                metaData = new ChatlogMetaData( ChatLogEntry.LogfilePath(chatlogDir, ircEntity) );
                chatlogs[ircEntity] = metaData;
            }

            var entry = new ChatLogEntry(metaData.LogfilePath, logMsg, args);

            LogIfDayChanged(metaData, entry);
            logWriter.Enqueue(entry);
            metaData.LastWrite = entry.Timestamp;

            Cleaner();
        }

        // Check if the day has changed since last write.
        void LogIfDayChanged(ChatlogMetaData metaData, ChatLogEntry entry)
        {
            if (metaData.LastWrite.Date != entry.Timestamp.Date)
            {
                logWriter.Enqueue(
                    new LogEntry(metaData.LogfilePath, entry.Timestamp,
                                 "--- Day has changed {0:ddd dd MMM yyyy}", entry.Timestamp));
            }
        }

        void Cleaner()
        {
            const int cleanInterval = 1000;

            if (logCount < cleanInterval)
                logCount++;
            else
            {
                logCount = 0;
                Clean();
            }
        }

        void Clean()
        {
            var timeLimit = TimeSpan.FromMinutes(10);

            var toRemove = new List<string>();
            var now = DateTimeOffset.Now;
            foreach (var pair in chatlogs)
            {
                var lastWrite = pair.Value.LastWrite;
                if ( (now - lastWrite) >= timeLimit )
                {
                    // When exceeding the timelimit, close the file so we regularly release filehandles.
                    logWriter.Enqueue( LogEntry.Close(pair.Value.LogfilePath) );
                    // Keep the metadata for a while longer, only delete it when there's a change in date.
                    // This ensures that LogIfDayChanged always correctly logs day changes.
                    if (lastWrite.Date < now.Date)
                    {
                        toRemove.Add(pair.Key);
                    }
                } // if
            } // foreach

            foreach (var key in toRemove)
                chatlogs.Remove(key);
        }


        void CloseLog(string ircEntity)
        {
            ChatlogMetaData metaData;
            if (chatlogs.TryGetValue(ircEntity, out metaData))
            {
                logWriter.Enqueue( LogEntry.Close(metaData.LogfilePath) );
            }
        }

    }
}