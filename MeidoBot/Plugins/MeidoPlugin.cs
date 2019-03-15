using System.Linq;
using System.Collections.Generic;
using MeidoCommon;
using MeidoCommon.ExtensionMethods;


namespace MeidoBot
{
    class MeidoPlugin
    {
        public readonly string Name;
        public readonly IEnumerable<Trigger> Triggers;
        public readonly IEnumerable<IIrcHandler> Handlers;
        public readonly IEnumerable<TopicHelp> Help;
        // All general help topics, also the ones just attached to a Trigger- or
        // CommandHelp (ie not included in `Help` above).
        public IEnumerable<TopicHelp> AllTopicHelp
        {
            get
            {
                // All help nodes of triggers and commands (of this plugin).
                var helpNodes =
                    from tr in Triggers
                    where tr.Help != null
                    from node in tr.Help.AllNodes()
                    select node;

                // All the Topics of all the help nodes.
                var topics =
                    from help in helpNodes.OfType<BaseHelp>()
                    from topic in help.AlsoSee
                    select topic;

                return Help.Concat(topics);
            }
        }


        public MeidoPlugin(IMeidoHook plugin)
        {
            Name = plugin.Name();

            var withTriggers = plugin as IPluginTriggers;
            var withHandlers = plugin as IPluginIrcHandlers;
            var withHelp = plugin as IPluginHelp;

            Triggers = withTriggers != null ?
                withTriggers.Triggers.NoNull() : new Trigger[0];

            Handlers = withHandlers != null ?
                withHandlers.IrcHandlers.NoNull() : new IIrcHandler[0];

            Help = withHelp != null ?
                withHelp.Help.NoNull() : new TopicHelp[0];
        }
    }
}