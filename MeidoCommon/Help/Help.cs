using System;
using System.Collections.Generic;


namespace MeidoCommon
{
    public abstract class BaseHelp
    {
        public readonly IEnumerable<string> Documentation;
        public IEnumerable<Help> SeeAlso { get; set; }


        internal BaseHelp(DynamicHelp dHelp)
        {
            Documentation = dHelp.Documentation;
        }
    }


    public class Help : BaseHelp
    {
        public readonly string Topic;


        Help(string topic, DynamicHelp dHelp) : base(dHelp)
        {
            if (topic == null)
                throw new ArgumentNullException(nameof(topic));
            
            Topic = topic;
        }

        public Help(string topic, string documentation)
            : this(topic, new DynamicHelp(documentation)) {}

        public Help(string topic, IEnumerable<string> documentation)
            : this(topic, new DynamicHelp(documentation)) {}

        public Help(string topic, Func<string> documentation)
            : this(topic, new DynamicHelp(documentation)) {}
    }
}