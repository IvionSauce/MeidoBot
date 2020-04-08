using System;


namespace MeidoBot
{
    /* This split between `LogEntry` and `ChatLogEntry` was originally envisioned to have `Logger` also use
     * LogEntry and the `LogWrite` backend. But I never felt comfortable with asynchronous logging for
     * the program/plugins, it could lead to a situation where pertinent information never makes it to disk
     * because of a crash. It's fine for chatlogs, but for the important logging I decided to keep it synchronous.
     * 
     * So why is the split still here? While the rest of the chatlogging subsystem got written one user of the
     * LogEntry base class emerged, `LogIfDayChanged`, which uses it to escape having the time prepended to the
     * final log message. This could be solved another way, but I like deferring most of the work until ToString is
     * called on the log writing thread. So it stays for now.
    */
    class LogEntry
    {
        public readonly DateTimeOffset Timestamp;
        public readonly string LogMessage;
        public readonly object[] FormatParams;
        public readonly string Filepath;


        public LogEntry(LogEntry entry, string path)
        {
            Timestamp = entry.Timestamp;
            LogMessage = entry.LogMessage;
            FormatParams = entry.FormatParams;
            Filepath = path;
        }

        public LogEntry(string path, string message, params object[] formatParams)
            : this(path, DateTimeOffset.Now, message, formatParams) {}

        public LogEntry(string path, DateTimeOffset timestamp, string message, params object[] formatParams)
        {
            Timestamp = timestamp;
            LogMessage = message;
            FormatParams = formatParams;
            Filepath = path;
        }


        public static LogEntry Close(string path)
        {
            return Close(path, DateTimeOffset.Now);
        }

        public static LogEntry Close(string path, DateTimeOffset now)
        {
            return new LogEntry(path, now, null, null);
        }

        public override string ToString()
        {
            return StandardFormat(LogMessage, FormatParams);
        }


        public static string StandardFormat(string message, params object[] formatParams)
        {
            if (formatParams != null && formatParams.Length > 0)
                return string.Format(message, formatParams);
            
            return message;
        }
    }


    class ChatLogEntry : LogEntry
    {
        public ChatLogEntry(LogEntry entry, string path) : base(entry, path) {}

        public ChatLogEntry(string path, string message, params object[] formatParams)
            : base(path, message, formatParams) {}

        public ChatLogEntry(string path, DateTimeOffset timestamp, string message, params object[] formatParams)
            : base(path, timestamp, message, formatParams) {}


        public override string ToString()
        {
            var msg = '[' + Timestamp.ToString("HH:mm:ss") + "] " + LogMessage;

            return StandardFormat(msg, FormatParams);
        }
    }
}