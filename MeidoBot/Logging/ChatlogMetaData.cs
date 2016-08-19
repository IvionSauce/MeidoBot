using System;


namespace MeidoBot
{
    class ChatlogMetaData
    {
        public readonly string LogfilePath;
        public DateTime LastWrite { get; set; }


        public ChatlogMetaData(string logfilePath)
        {
            LogfilePath = logfilePath;
            LastWrite = DateTime.MaxValue;
        }
    }
}