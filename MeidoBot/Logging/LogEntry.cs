using System;


namespace MeidoBot
{
    class LogEntry
    {
        public readonly DateTime Timestamp;
        public readonly string LogMessage;
        public readonly object[] FormatParams;
        public readonly string Filepath;


        public LogEntry(string path, string message, params object[] formatParams)
            : this(path, DateTime.Now, message, formatParams) {}

        public LogEntry(string path, DateTime timestamp, string message, params object[] formatParams)
        {
            Timestamp = timestamp;
            LogMessage = message;
            FormatParams = formatParams;
            Filepath = path;
        }


        public static LogEntry Close(string path)
        {
            return new LogEntry(path, null, null);
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
        public ChatLogEntry(string path, string message, params object[] formatParams)
            : base(path, message, formatParams) {}


        public override string ToString()
        {
            var msg = '[' + Timestamp.ToString("HH:mm:ss") + "] " + LogMessage;

            return StandardFormat(msg, FormatParams);
        }


        public static string LogfilePath(string basePath, string ircEntity)
        {
            // Sanitize name of irc entity so it's a valid filename. This will probably be expanded in the future.
            string filename = ircEntity.Replace('/', '_');

            return System.IO.Path.Combine(basePath, filename);
        }
    }
}