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

        int logCount;



        public ChatWriter(LogWriter writer, string chatlogDir)
        {
            logWriter = writer;
            this.chatlogDir = chatlogDir;
        }


        public void Log(string ircEntity, string logMsg, params object[] args)
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


        public void CloseLog(string ircEntity)
        {
            ChatlogMetaData metaData;
            if (chatlogs.TryGetValue(ircEntity, out metaData))
            {
                logWriter.Enqueue( LogEntry.Close(metaData.LogfilePath) );
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
    }
}