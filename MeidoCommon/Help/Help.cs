using System;
using System.Collections.Generic;
using MeidoCommon.ExtensionMethods;


namespace MeidoCommon
{
    // Uniform travelling when dealing with Trigger- and CommandHelp.
    public interface IHelpNode
    {
        IHelpNode Parent { get; }
        IEnumerable<IHelpNode> Siblings { get; }
        IEnumerable<IHelpNode> Children { get; }
    }


    public abstract class BaseHelp
    {
        public readonly IEnumerable<string> Documentation;
        IEnumerable<Help> _alsoSee;
        public IEnumerable<Help> AlsoSee
        {
            get { return _alsoSee.NoNull(); }
            set { _alsoSee = value; }
        }


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