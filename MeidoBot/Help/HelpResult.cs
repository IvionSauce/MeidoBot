﻿using System;
using System.Linq;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class HelpResult
    {
        public static readonly HelpResult Failure = new HelpResult();

        public bool Success
        {
            get { return firstLine != null; }
        }

        const string helpSep = " – ";

        readonly string firstLine;
        readonly List<string> restLines;


        public HelpResult()
        {
            // Leave everything default.
        }

        public HelpResult(BaseHelp bHelp, string footer)
        {
            const string noHelp = "No help available.";

            firstLine = bHelp.Documentation
                             .FirstOrDefault() ?? noHelp;

            restLines = bHelp.Documentation
                             .Skip(1).ToList();

            if (!string.IsNullOrEmpty(footer))
                restLines.Add(footer);
        }

        //public HelpResult(TopicHelp help, string footer) : this((BaseHelp)help, footer)
        //{
        //    firstLine = help.Topic + helpSep + firstLine;
        //}

        public HelpResult(
            HelpRequest req,
            CommandBaseHelp cmdBaseHelp,
            string footer) : this(cmdBaseHelp, footer)
        {
            firstLine = Format(
                Signature(req, cmdBaseHelp.Parameters),
                firstLine
            );
        }


        static string Format(string signature, string firstLine)
        {
            return signature + helpSep + firstLine;
        }

        // Signature for trigger/command help.
        static string Signature(HelpRequest req, string parameters)
        {
            string prefix = null;
            if (!req.StartsWithTriggerPrefix)
                prefix = req.TriggerPrefix;
            
            if (!string.IsNullOrEmpty(parameters))
            {
                return prefix + req.NormalizedQuery +
                       " " + parameters;
            }

            return prefix + req.NormalizedQuery;
        }


        public void PrintHelp(ITriggerMsg msg)
        {
            if (!Success)
                throw new InvalidOperationException("Success is false, no help to be printed.");

            const int softLimit = 3;
            const int hardLimit = 10;

            // Always send first line regularly.
            msg.Reply(firstLine);

            if (restLines.Count > 0)
            {
                // Send the other lines via notice if there are too many and
                // help was requested in a channel.
                Action<string> send;
                if (restLines.Count > softLimit && msg.Channel != null)
                    send = msg.SendNotice;
                else
                    send = msg.Reply;

                // Never send more than the hard limit.
                int stop = restLines.Count;
                if (stop > hardLimit)
                    stop = hardLimit;

                for (int count = 0; count < stop; count++)
                    send(restLines[count]);
            }
        }
    }
}