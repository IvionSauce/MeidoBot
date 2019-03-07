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
            Triggers = plugin.Triggers.NoNull();

            var withHandlers = plugin as IPluginIrcHandlers;
            if (withHandlers != null)
                Handlers = withHandlers.IrcHandlers.NoNull();
            else
                Handlers = new IIrcHandler[0];
        }
    }
}