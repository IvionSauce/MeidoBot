using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class PluginTriggers
    {
        public readonly string Name;
        public readonly IEnumerable<Trigger> Triggers;


        public PluginTriggers(IMeidoHook plugin)
        {
            Name = plugin.Name;
            Triggers = plugin.Triggers;
        }
    }
}