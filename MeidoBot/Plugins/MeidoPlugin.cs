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


        public MeidoPlugin(IMeidoHook plugin)
        {
            Name = plugin.Name();

            var withTriggers = plugin as IPluginTriggers;
            var withHandlers = plugin as IPluginIrcHandlers;
            var withHelp = plugin as IPluginHelp;

            Triggers = withTriggers?.Triggers.NoNull();
            Handlers = withHandlers?.IrcHandlers.NoNull();
            Help = withHelp?.Help.NoNull();
        }
    }
}