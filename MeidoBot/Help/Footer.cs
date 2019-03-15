using System.Text;
using System.Linq;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class Footer
    {
        readonly Triggers triggers;

        // Consts for formatting footer.
        const string pathSep = " ";
        const string sectionSep = "   ";
        const string alsoSection = "▶ Also see: ";
        const string altTrigSection = "⬟ Alt: ";
        const string relatedSection = "◆ Related: ";


        public Footer(Triggers triggers)
        {
            this.triggers = triggers;
        }


        public string Format(HelpRequest req, BaseHelp help)
        {
            return FormatFooter(req.First, help);
        }

        // --- Formatting footer methods ---
        // These can be a bit involved due to all the tree traversal and filtering.

        string FormatFooter(string triggerId, BaseHelp help)
        {
            var sb = new StringBuilder();

            var trHelp = help as TriggerHelp;
            var cmdHelp = help as CommandHelp;
            if (trHelp != null)
                TriggerFooter(sb, triggerId, trHelp);
            if (cmdHelp != null)
                CommandFooter(sb, triggerId, cmdHelp);

            // Common for all help types.
            var alsoSee = string.Join(
                Help.ListSep, help.AlsoSee.Select(h => h.Topic)
            );
            if (alsoSee != string.Empty)
            {
                if (sb.Length > 0)
                    sb.Append(sectionSep);

                sb.Append(alsoSection).Append(alsoSee);
            }

            return sb.ToString();
        }


        void TriggerFooter(StringBuilder sb, string triggerId, TriggerHelp help)
        {
            var trigger = help.ParentTrigger;

            // Select trigger identifiers that are for this trigger and
            // filter out the current trigger id.
            var alternativeIds =
                from id in trigger.Identifiers
                where triggers.IsRegisteredAs(trigger, id)
                where id != triggerId
                select id;

            // For each related trigger select the first identifier that is known to us (prime id).
            var relatedTriggers = triggers.PrimeIds(trigger.RelatedTriggers);
            // The possible commands might also be pertinent.
            var commands = help.Commands.Select(cmd => triggerId + pathSep + cmd.Command);

            var alternative = Join(triggers.Prefix, alternativeIds);
            var related = Join(
                triggers.Prefix,
                relatedTriggers.Concat(commands)
            );

            if (alternative != string.Empty)
                sb.Append(altTrigSection).Append(alternative);

            if (related != string.Empty)
            {
                if (sb.Length > 0)
                    sb.Append(sectionSep);

                sb.Append(relatedSection).Append(related);
            }
        }


        void CommandFooter(StringBuilder sb, string triggerId, CommandHelp help)
        {
            var cmdPath = new Stack<string>();
            // Push empty string such that the later call to string.Join will result
            // in a space at the end.
            cmdPath.Push(string.Empty);

            IHelpNode startingNode;
            var relatedCommands = GetRelated(help, out startingNode);
            // Build command path up to the root.
            StackCommandPath(cmdPath, startingNode, triggers.Prefix + triggerId);

            // Prefix is "trigger [command [subcommand...]] ".
            var prefix = string.Join(pathSep, cmdPath);
            var related = Join(prefix, relatedCommands);

            if (related != string.Empty)
                sb.Append(relatedSection).Append(related);
        }

        // Returns related commands and their parent node, which is where we will start
        // to build the prefix.
        static IEnumerable<string> GetRelated(CommandHelp help, out IHelpNode parentNode)
        {
            IEnumerable<string> related;

            // Assume subcommands and sibling commands are relevant.
            if (help.Siblings.Any())
            {
                var siblings =
                    from siblingHelp in help.Siblings.Cast<CommandHelp>()
                    select siblingHelp.Command;

                var children = help.Subcommands
                                   .Select(sc => help.Command + pathSep + sc.Command);

                related = children.Concat(siblings);
                parentNode = help.Parent;
            }
            else
            {
                related = help.Subcommands.Select(sc => sc.Command);
                parentNode = help;
            }

            return related;
        }

        static void StackCommandPath(Stack<string> cmdPath, IHelpNode start, string root)
        {
            // Move backwards up the IHelpNode tree to construct the full command.
            var currentNode = start;
            while (currentNode != null)
            {
                var cmdHelp = currentNode as CommandHelp;
                if (cmdHelp != null)
                {
                    cmdPath.Push(cmdHelp.Command);
                    currentNode = cmdHelp.Parent;
                }
                else
                    break;
            }
            // We now have the command path, _not including_ the root, ie trigger.
            // So that's the final push.
            cmdPath.Push(root);
        }


        static string Join(string prefix, IEnumerable<string> items)
        {
            return string.Join(
                Help.ListSep,
                items.Select(item => prefix + item)
            );
        }
    }
}