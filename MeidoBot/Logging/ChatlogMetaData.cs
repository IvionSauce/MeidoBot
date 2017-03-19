using System;


namespace MeidoBot
{
    class ChatlogMetaData
    {
        public readonly string LogfilePath;
        public DateTimeOffset LastWrite { get; set; }


        public ChatlogMetaData(string logfilePath)
        {
            LogfilePath = logfilePath;
            LastWrite = DateTimeOffset.MaxValue;
        }
    }
}