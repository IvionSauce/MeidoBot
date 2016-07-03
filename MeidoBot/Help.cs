﻿using System;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class Help
    {
        readonly string triggerPrefix;
        readonly Dictionary<string, string> subjects =
            new Dictionary<string, string> (StringComparer.Ordinal);


        public Help(string triggerPrefix)
        {
            this.triggerPrefix = triggerPrefix;
        }


        public void RegisterHelp(IEnumerable< Dictionary<string, string> > helpDicts)
        {
            foreach (var dict in helpDicts)
            {
                RegisterHelp(dict);
            }
        }

        public void RegisterHelp(Dictionary<string, string> helpDict)
        {
            foreach (var pair in helpDict)
            {
                subjects[pair.Key] = pair.Value;
            }
        }


        public void Trigger(IIrcMessage msg)
        {
            string subject = null;
            if (msg.MessageArray.Length > 1)
                subject = string.Join(" ", msg.MessageArray, 1, msg.MessageArray.Length - 1);

            if (string.IsNullOrWhiteSpace(subject))
            {
                var helpSubjects = string.Join(", ", GetHelpSubjects());

                msg.Reply("Help is available on - " + helpSubjects);
            }
            else
            {
                string help = GetHelp(subject);

                if (help != null)
                    msg.Reply(help);
                else
                    msg.Reply("No help available.");
            }
        }


        string GetHelp(string subject)
        {
            string helpSubject = subject.Trim();

            if (helpSubject.StartsWith(triggerPrefix, StringComparison.Ordinal))
            {
                helpSubject = subject.Substring(triggerPrefix.Length);
            }

            string help;
            if (subjects.TryGetValue(helpSubject, out help))
            {
                return OptionallyPrependPrefix(helpSubject, help, triggerPrefix);
            }

            return null;
        }

        static string OptionallyPrependPrefix(string subject, string help, string triggerPrefix)
        {
            if (help.StartsWith(subject, StringComparison.Ordinal))
            {
                return triggerPrefix + help;
            }

            return help;
        }


        string[] GetHelpSubjects()
        {
            var subjectKeys = new List<string>();
            foreach (var key in subjects.Keys)
            {
                subjectKeys.Add(key);
            }

            subjectKeys.Sort();
            return subjectKeys.ToArray();
        }
    }
}