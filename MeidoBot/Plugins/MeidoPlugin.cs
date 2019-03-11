using System.Collections.Generic;
using MeidoCommon;
using MeidoCommon.ExtensionMethods;


namespace MeidoBot
{
    class MeidoPlugin
    {
        public readonly string Name;
        public readonly Dictionary<string, string> Help;
        public readonly IEnumerable<Trigger> Triggers;
        public readonly IEnumerable<IIrcHandler> Handlers;


        public MeidoPlugin(IMeidoHook plugin)
        {
            Name = plugin.Name();
            Help = plugin.Help();

            var withTriggers = plugin as IPluginTriggers;
            var withHandlers = plugin as IPluginIrcHandlers;

            Triggers = withTriggers?.Triggers.NoNull();
            Handlers = withHandlers?.IrcHandlers.NoNull();
        }
    }
}