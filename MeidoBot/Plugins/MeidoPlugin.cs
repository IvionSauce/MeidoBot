﻿using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class MeidoPlugin
    {
        public readonly string Name;
        public readonly Dictionary<string, string> Help;
        public readonly IEnumerable<Trigger> Triggers;


        public MeidoPlugin(IMeidoHook plugin)
        {
            Name = plugin.Name();
            Help = plugin.Help();
            Triggers = plugin.Triggers();
        }
    }
}