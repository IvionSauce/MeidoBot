using System;
using System.Collections.Generic;
using MeidoCommon.ExtensionMethods;


namespace MeidoCommon
{
    public class TopicHelp : BaseHelp
    {
        public readonly string Topic;


        TopicHelp(string topic, DynamicHelp dHelp) : base(dHelp)
        {
            if (topic == null)
                throw new ArgumentNullException(nameof(topic));
            
            Topic = topic;
        }

        public TopicHelp(string topic, string documentation)
            : this(topic, new DynamicHelp(documentation)) {}

        public TopicHelp(string topic, IEnumerable<string> documentation)
            : this(topic, new DynamicHelp(documentation)) {}

        public TopicHelp(string topic, Func<string> documentation)
            : this(topic, new DynamicHelp(documentation)) {}
    }
}