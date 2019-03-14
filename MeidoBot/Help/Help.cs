using System;
using System.Linq;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class Help
    {
        readonly Triggers triggers;
        readonly Dictionary<string, TopicHelp> helpOnTopics;
        readonly Footer footer;

        // Consts for formatting purposes.
        public const string NoHelpError = "No help available.";
        public const string ListSep = ", ";


        public Help(Triggers triggers)
        {
            this.triggers = triggers;
            helpOnTopics = new Dictionary<string, TopicHelp>(StringComparer.Ordinal);
            footer = new Footer(triggers);
        }


        public void RegisterHelp(MeidoPlugin plugin)
        {
            foreach (var help in plugin.Help)
                helpOnTopics[help.Topic] = help;
        }


        public void Trigger(ITriggerMsg msg)
        {
            var request = HelpRequest.FromHelpTrigger(msg.MessageArray, triggers.Prefix);
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
                irc.SendMessage(msg.ReturnTo, "Triggers: " + string.Join(ListSep, trigIds));
                irc.SendMessage(msg.ReturnTo, "Other: " + string.Join(ListSep, topics));
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
                    result = request.ToResult(help, footer);
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
                    return request.ToResult(cmdHelp, footer);
                }
            }
            // Help query for just a trigger.
            else
            {
                return request.ToResult(trHelp, footer);
            }

            return HelpResult.Failure;
        }
    }
}