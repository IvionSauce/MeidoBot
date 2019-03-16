using System;
using System.Linq;
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


        public static TriggerHelp HelpNullWrap(this Trigger tr, string errorMsg)
        {
            var help = tr.Help;
            if (help == null)
            {
                help = new TriggerHelp(errorMsg);
                tr.Help = help;
            }

            return help;
        }


        public static IEnumerable<IHelpNode> AllNodes<T>(this T root)
            where T : IHelpNode
        {
            // Root is included in all nodes.
            var nodes = new IHelpNode[] {root};
            return RecurseNodes(Enumerable.Empty<IHelpNode>(), nodes);
        }

        static IEnumerable<IHelpNode> RecurseNodes(IEnumerable<IHelpNode> acc, IEnumerable<IHelpNode> current)
        {
            if (current.Any())
            {
                // For each node, get child nodes.
                var allChildNodes =
                    from node in current
                    from child in node.Children
                    select child;

                // Oh holy stack, lead us not into uneliminated tail recursion,
                // but deliver us from overflows.
                return RecurseNodes(acc.Concat(current), allChildNodes);
            }
            else
                return acc;
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