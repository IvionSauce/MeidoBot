using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MeidoCommon;


namespace MeidoBot
{
    public static class PluginExtensions
    {
        public static string Name(this IMeidoHook plugin)
        {
            string name = plugin.Name.Trim();
            switch (name)
            {
                case "":
                case null:
                name = "Unknown";
                break;
                // Reserved names.
                case "Main":
                case "Meido":
                case "Triggers":
                case "Auth":
                name = "_" + name;
                break;
            }

            return name;
        }

        public static Dictionary<string, string> Help(this IMeidoHook plugin)
        {
            if (plugin.Help != null)
                return plugin.Help;

            return new Dictionary<string, string>();
        }


        public static bool Query(this TriggerHelp root, string[] fullCommand, out CommandHelp help)
        {
            help = null;
            var cmdHelpNodes = root.Commands;
            foreach (string cmdPart in fullCommand)
            {
                if (TryGetHelp(cmdPart, cmdHelpNodes, out help))
                    cmdHelpNodes = help.Subcommands;
                else
                {
                    help = null;
                    break;
                }
            }

            return help != null;
        }

        static bool TryGetHelp(
            string command,
            ReadOnlyCollection<CommandHelp> searchSpace,
            out CommandHelp help)
        {
            foreach (var cmdHelp in searchSpace)
            {
                if (cmdHelp.Command.Equals(command, StringComparison.Ordinal))
                {
                    help = cmdHelp;
                    return true;
                }
            }

            help = null;
            return false;
        }
    }
}