using System;
using MeidoCommon;


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

        public HelpRequest(string helpQuery, string triggerPre)
        {
            var split = helpQuery.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 0)
            {
                // Read first element and check if it's a trigger. If it is, remove prefix and
                // qualify that we're searching for trigger help.
                First = split[0];
                if (First.StartsWith(triggerPre, StringComparison.Ordinal))
                {
                    First = First.Substring(triggerPre.Length);
                    RestrictToTriggers = true;
                }
                // Read the remaining elements.
                if (split.Length > 1)
                {
                    Rest = new string[split.Length - 1];
                    Array.Copy(split, 1, Rest, 0, Rest.Length);
                }

                NormalizedQuery = string.Join(" ", split);
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


        public static HelpRequest FromHelpTrigger(string[] ircMsg, string triggerPre)
        {
            if (ircMsg.Length > 1)
            {
                var query = string.Join(" ", ircMsg, 1, ircMsg.Length - 1);
                return new HelpRequest(query, triggerPre);
            }

            return new HelpRequest();
        }
    }
}