using System;
using MeidoCommon;
using MeidoCommon.Parsing;


namespace MeidoBot
{
    class HelpRequest
    {
        public readonly string First;
        public readonly string[] Rest;
        public readonly string NormalizedQuery;
        public readonly string TriggerPrefix;
        public readonly bool RestrictToTriggers;

        public bool IsValid
        {
            get { return NormalizedQuery != null; }
        }
        public bool StartsWithTriggerPrefix
        {
            get { return RestrictToTriggers; }
        }
        public bool HasRest
        {
            get { return Rest != null; }
        }


        public HelpRequest()
        {
            // Leave everyting default.
        }

        HelpRequest(string[] query, string triggerPre)
        {
            if (query.Length > 0)
            {
                // Read first element and check if it's a trigger. If it is, remove prefix and
                // qualify that we're searching for trigger help.
                First = query[0];
                if (First.StartsWith(triggerPre, StringComparison.Ordinal))
                {
                    First = First.Substring(triggerPre.Length);
                    RestrictToTriggers = true;
                }
                // Read the remaining elements.
                if (query.Length > 1)
                {
                    Rest = new string[query.Length - 1];
                    Array.Copy(query, 1, Rest, 0, Rest.Length);
                }

                NormalizedQuery = string.Join(" ", query);
            }
            TriggerPrefix = triggerPre;
        }


        public HelpResult ToResult(TopicHelp help, Footer foot)
        {
            return new HelpResult(
                help,
                foot.Format(this, help)
            );
        }

        public HelpResult ToResult(CommandBaseHelp cmdBaseHelp, Footer foot)
        {
            return new HelpResult(
                this,
                cmdBaseHelp,
                foot.Format(this, cmdBaseHelp)
            );
        }


        public static HelpRequest FromHelpTrigger(ITriggerMsg msg, string triggerPre)
        {
            var args = msg.ArgArray();
            if (args.Length > 1)
                return new HelpRequest(args, triggerPre);

            return new HelpRequest();
        }
    }
}