using System;
using System.Collections.Generic;


namespace MeidoBot
{
    class ChatWriter
    {
        readonly LogWriter logWriter;

        readonly string chatlogDir;
        readonly Dictionary<string, ChatlogMetaData> chatlogs =
            new Dictionary<string, ChatlogMetaData>(StringComparer.OrdinalIgnoreCase);

        static TimeSpan cleanInterval = TimeSpan.FromHours(24);
        static TimeSpan chatTimeout = TimeSpan.FromMinutes(10);

        DateTimeOffset lastClean;


        public ChatWriter(LogWriter writer, string chatlogDir)
        {
            logWriter = writer;
            this.chatlogDir = chatlogDir;
            lastClean = DateTimeOffset.Now;
        }


        public void Log(string ircEntity, string logMsg, params object[] args)
        {
            // We need the datetime for the metadata, logentry and cleaning algorithm.
            var now = DateTimeOffset.Now;

            var metaData = GetMetaData(ircEntity, now);
            var entry = new ChatLogEntry(metaData.LogfilePath, now, logMsg, args);

            if (metaData.LogRotate(now))
            {
                // Close old log file.
                logWriter.Enqueue( LogEntry.Close(entry.Filepath, now) );
                // Update entry to new path.
                entry = new ChatLogEntry(entry, metaData.LogfilePath);
            }

            LogIfDayChanged(metaData, entry);
            logWriter.Enqueue(entry);
            metaData.LastWrite = entry.Timestamp;

            Clean(now);
        }

        ChatlogMetaData GetMetaData(string ircEntity, DateTimeOffset now)
        {
            if (!chatlogs.TryGetValue(ircEntity, out ChatlogMetaData metaData))
            {
                // Rotate non-channel logs more leisurely.
                var rotateSched = MessageTools.IsChannel(ircEntity) ?
                    LogRotateSchedule.Daily : LogRotateSchedule.Yearly;

                metaData = new ChatlogMetaData(
                    ChatLogEntry.LogfilePath(chatlogDir, ircEntity),
                    rotateSched, now
                );
                chatlogs[ircEntity] = metaData;
            }

            return metaData;
        }

        void LogIfDayChanged(ChatlogMetaData metaData, LogEntry entry)
        {
            if (metaData.LastWrite.Date != entry.Timestamp.Date)
            {
                logWriter.Enqueue(
                    new LogEntry(
                        metaData.LogfilePath, entry.Timestamp,
                        "--- Day has changed {0:ddd dd MMM yyyy}", entry.Timestamp)
                );
            }
        }


        public void CloseLog(string ircEntity)
        {
            if (chatlogs.TryGetValue(ircEntity, out ChatlogMetaData metaData))
            {
                logWriter.Enqueue( LogEntry.Close(metaData.LogfilePath) );
            }
        }


        void Clean(DateTimeOffset now)
        {
            if ((now - lastClean) >= cleanInterval)
            {
                var toRemove = new List<string>();

                foreach (var pair in chatlogs)
                    Clean(pair, now, toRemove);

                foreach (var key in toRemove)
                    chatlogs.Remove(key);
            }
        }

        void Clean(KeyValuePair<string, ChatlogMetaData> pair, DateTimeOffset now, List<string> expired)
        {
            var chatlog = pair.Value;
            var delta = now - chatlog.LastWrite;

            if (chatlog.Schedule != LogRotateSchedule.Daily)
            {
                // Regularly release filehandles by closing logs that don't rotate regularly themselves.
                if (delta >= chatTimeout)
                {
                    logWriter.Enqueue(LogEntry.Close(chatlog.LogfilePath, now));
                    // Keep the metadata for a while longer, this ensures that `LogIfDayChanged` always
                    // logs day changes correctly.
                    if (chatlog.LastWrite.Date < now.Date)
                        expired.Add(pair.Key);
                }
            }
            // Always cleanup if the last write was too long ago.
            else if (delta > cleanInterval)
            {
                logWriter.Enqueue(LogEntry.Close(chatlog.LogfilePath, now));
                expired.Add(pair.Key);
            }
        }

    }
}