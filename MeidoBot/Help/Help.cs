using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class Help
    {
        readonly Triggers triggers;
        readonly Dictionary<string, TopicHelp> helpOnTopics;
        string triggerPrefix
        {
            get { return triggers.Prefix; }
        }

        // Consts for formatting footer.
        public const string NoHelpError = "No help available.";
        const string pathSep = " ";
        const string listSep = ", ";
        const string sectionSep = "   ";
        const string alsoSection = "▶ Also see: ";
        const string altTrigSection = "⬟ Alt: ";
        const string relatedSection = "◆ Related: ";


        public Help(Triggers triggers)
        {
            this.triggers = triggers;
            helpOnTopics = new Dictionary<string, TopicHelp>(StringComparer.Ordinal);
        }


        public void RegisterHelp(MeidoPlugin plugin)
        {
            foreach (var help in plugin.Help)
                helpOnTopics[help.Topic] = help;
        }


        public void Trigger(ITriggerMsg msg)
        {
            var request = HelpRequest.FromHelpTrigger(msg.MessageArray, triggerPrefix);
            // Help trigger with query: lookup query and print help.
            if (request.IsValid)
            {
                var result = DoRequest(request);
                if (result.Success)
                    result.PrintHelp(msg);
                else
                    msg.Reply("Sorry, I couldn't find any help for '{0}'.",
                              request.NormalizedQuery);
            }
            // Just the help trigger, no query: print all the subjects we've got.
            else
            {
                var trigIds =
                    from id in triggers.PrimeIdentifiers
                    orderby id
                    select id;
                
                var topics =
                    from help in helpOnTopics.Values
                    orderby help.Topic
                    select help.Topic;

                var irc = msg.Irc;
                irc.SendMessage(msg.ReturnTo, "Triggers: " + string.Join(listSep, trigIds));
                irc.SendMessage(msg.ReturnTo, "Other: " + string.Join(listSep, topics));
            }
        }


        // --- Request handling ---

        HelpResult DoRequest(HelpRequest request)
        {
            var result = HelpResult.Failure;

            Trigger tr;
            if (triggers.TryGet(request.First, out tr))
            {
                // We'll wrap Help so that even when there's no help for a trigger
                // a footer is still made (with related triggers).
                result = GetHelp(request, tr.HelpNullWrap(NoHelpError));
            }

            // If it's not exclusively a trigger/command query try to find the query
            // as a general help topic.
            if ( !(result.Success || request.RestrictToTriggers) )
            {
                TopicHelp help;
                if (helpOnTopics.TryGetValue(request.NormalizedQuery, out help))
                {
                    result = new HelpResult(help, FormatFooter(help));
                }

            }

            return result;
        }

        HelpResult GetHelp(HelpRequest request, TriggerHelp trHelp)
        {
            // Help query for a trigger+command.
            if (request.HasRest)
            {
                // Check if we can find a command corresponding to the query.
                // Return help for command if we do.
                CommandHelp cmdHelp;
                if (trHelp.Query(request.Rest, out cmdHelp))
                {
                    return request.ToResult(
                        cmdHelp,
                        FormatFooter(request.First, cmdHelp)
                    );
                }
            }
            // Help query for just a trigger.
            else
            {
                return request.ToResult(
                    trHelp,
                    FormatFooter(request.First, trHelp)
                );
            }

            return HelpResult.Failure;
        }


        // --- Formatting footer methods ---
        // These can be a bit involved due to all the tree traversal and filtering.

        string FormatFooter(TopicHelp help)
        {
            return FormatFooter(null, help);
        }

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
                listSep, help.AlsoSee.Select(h => h.Topic)
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

            // Select all trigger identifiers that are not the current one.
            var alternativeIds =
                trigger.Identifiers.Where(id => id != triggerId);

            // For each related trigger select the first identifier that is known to us (prime id).
            var relatedTriggers = triggers.PrimeIds(trigger.RelatedTriggers);
            // The possible commands might also be pertinent.
            var commands = help.Commands.Select(cmd => triggerId + pathSep + cmd.Command);

            var alternative = Join(triggerPrefix, alternativeIds);
            var related = Join(
                triggerPrefix,
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
            StackCommandPath(cmdPath, startingNode, triggerPrefix + triggerId);

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
                listSep,
                items.Select(item => prefix + item)
            );
        }
    }
}