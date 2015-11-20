using System;
using System.Collections.Generic;


namespace MeidoBot
{
    class ThrottleManager
    {
        readonly Dictionary<string, SourceEntry> sources =
            new Dictionary<string, SourceEntry>(StringComparer.OrdinalIgnoreCase);
        
        readonly Logger log;


        public ThrottleManager(Logger log)
        {
            this.log = log;
        }


        // Returns true if trigger calls are allowed. Returns false otherwise.
        public bool AllowTriggers(IrcMessage msg)
        {
            return !Triggers(msg);
        }

        // Returns true if trigger calls should be throttled. Returns false otherwise.
        public bool Triggers(IrcMessage msg)
        {
            var entry = GetOrAdd(msg.ReturnTo);
            // Trigger calling happens sequentially, no need for locking.

            // When trigger throttling is currently active, inform the one calling of that fact.
            if (entry.Triggers.ThrottleActive)
            {
                msg.SendNotice("Sorry, currently ignoring trigger calls from {0}. Time remaining: {1}",
                    msg.ReturnTo, entry.Triggers.TimeLeft);
                return true;
            }

            ThrottleInfo info;
            // If this trigger call causes the throttle to activate, share information about why and how long trigger
            // calling will be ignored.
            if (entry.Triggers.Check(out info))
            {
                var report =
                    string.Format("Ignoring trigger calls from {0} for {1} minutes. ({2} calls in {3} seconds)",
                        msg.ReturnTo, info.ThrottleDuration.TotalMinutes,
                        info.Limit, info.Interval.TotalSeconds);
                
                msg.Reply(report);
                log.Message(report);
                return true;
            }

            return false;
        }


        // Returns true if output is allowed. Returns false otherwise.
        public bool AllowOutput(string target, Action<string> notify)
        {
            return !Output(target, notify);
        }

        // Returns true if output should be throttled. Returns false otherwise.
        public bool Output(string target, Action<string> notify)
        {
            var entry = GetOrAdd(target);
            ThrottleInfo info;
            // Lock on output checks, since plugins can be multithreaded and therefore messages to be send can arrive
            // simultaneously and cause OutputCheck to be called concurrently.
            lock (entry.Output)
            {
                if (entry.Output.ThrottleActive)
                    return true;

                info = entry.Output.Check();
            }

            // If this outward message causes the throttle to activate, share information about why and how long we are
            // going to stay silent (to prevent flooding/spam).
            if (info != null)
            {
                log.Message("Halting messages and notices to {0} for {1} minutes. ({2} messages in {3} seconds)",
                    target, info.ThrottleDuration.TotalMinutes,
                    info.Limit, info.Interval.TotalSeconds);

                notify(string.Format("Sorry for the spam, either something went wrong or I'm being abused. " +
                    "Going silent for {0} minutes. ({1} messages in {2} seconds)",
                    info.ThrottleDuration, info.Limit, info.Interval.TotalSeconds));

                return true;
            }

            return false;
        }


        SourceEntry GetOrAdd(string source)
        {
            // Serialize access to the dictionary, courtesy of the possible concurrent use of OutputCheck.
            lock (sources)
            {
                SourceEntry entry;
                if (!sources.TryGetValue(source, out entry))
                {
                    entry = new SourceEntry();
                    sources[source] = entry;
                }

                return entry;
            }
        }
    }


    class SourceEntry
    {
        public readonly ThrottleControl Triggers;
        public readonly ThrottleControl Output;


        public SourceEntry()
        {
            const int bigLimit = 15;
            var bigInterval = TimeSpan.FromSeconds(15);

            var triggerRates = new RateControl[]
            {
                new RateControl(5, 2),
                new RateControl(bigLimit, bigInterval)
            };

            var outputRates = new RateControl[]
            {
                new RateControl(10, 5),
                new RateControl(bigLimit, bigInterval)
            };

            var duration = TimeSpan.FromMinutes(15);

            Triggers = new ThrottleControl(triggerRates, duration);
            Output = new ThrottleControl(outputRates, duration);
        }
    }
}