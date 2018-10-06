using System;
using System.Collections.Generic;
using MeidoCommon;
using MeidoCommon.Throttle;


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
        public bool AllowTriggers(IIrcMessage msg)
        {
            return !Triggers(msg);
        }

        // Returns true if trigger calls should be throttled. Returns false otherwise.
        public bool Triggers(IIrcMessage msg)
        {
            var entry = GetOrAdd(msg.ReturnTo);
            ThrottleInfo info;
            // Lock on trigger calling. Trigger calling is also possibly multithreaded,
            // because of the varying threading models now offered in MessageDispatcher.
            lock (entry.Triggers)
            {
                // Checks of both Triggers and Output if their throttle is active.
                if (ThrottleActive(msg, entry))
                    return true;

                info = entry.Triggers.Check();
            }

            // If this trigger call causes the throttle to activate, share information about
            // why and how long trigger calling will be ignored.
            if (info != null)
            {
                var report =
                    string.Format( "Ignoring trigger calls from {0} for {1}. ({2} calls in {3})",
                        msg.ReturnTo, Short(info.ThrottleDuration),
                        info.MessageLimit, Short(info.Interval) );
                
                log.Message(report);
                msg.Reply(report);
                return true;
            }

            return false;
        }

        static bool ThrottleActive(IIrcMessage msg, SourceEntry entry)
        {
            if (entry.Triggers.ThrottleActive)
            {
                msg.SendNotice("Sorry, currently ignoring trigger calls from {0}. Time remaining: {1}",
                               msg.ReturnTo, entry.Triggers.TimeLeft);
                return true;
            }

            // Lock on output checks.
            lock (entry.Output)
            {
                // Refrain from doing any trigger work when output is being throttled anyways. This is not 100% safe,
                // since some trigger calls might not output to originating channel/user (ReturnTo), but is worth it to
                // protect against abuse.
                if (entry.Output.ThrottleActive)
                {
                    // Only attempt to send if source (ReturnTo) of this trigger call isn't the nick, since that would
                    // mean its subject to the active throttle which we just checked.
                    if (msg.ReturnTo != msg.Nick)
                    {
                        msg.SendNotice("Sorry, currently staying silent in {0}. Time remaining: {1}",
                                       msg.ReturnTo, entry.Output.TimeLeft);
                    }

                    return true;
                }
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
            // simultaneously and cause Output.Check to be called concurrently.
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
                log.Message( "Halting messages and notices to {0} for {1}. ({2} messages in {3})",
                    target, Short(info.ThrottleDuration),
                    info.MessageLimit, Short(info.Interval) );

                notify( string.Format("Sorry for the spam, either something went wrong or I'm being abused. " +
                    "Going silent for {0}. ({1} messages in {2})",
                    Short(info.ThrottleDuration), info.MessageLimit, Short(info.Interval)) );

                return true;
            }

            return false;
        }


        string Short(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return duration.TotalHours + "h";
            if (duration.TotalMinutes >= 1)
                return duration.TotalMinutes + "m";

            return duration.TotalSeconds + "s";
        }


        SourceEntry GetOrAdd(string source)
        {
            // Serialize access to the dictionary.
            lock (sources)
            {
                SourceEntry entry;
                if (!sources.TryGetValue(source, out entry))
                {
                    entry = new SourceEntry(source);
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

        // Lots of magic numbers here, my apologies. These are subject to experimentation and refinement. Eventually
        // they will be configurable, but I can't yet be arsed.
        public SourceEntry(string source)
        {
            var triggerRates = new RateControl[] {
                RateControl.FromSeconds(6, 30),
                RateControl.FromSeconds(8, 60)
            };

            RateControl[] outputRates;
            if (MessageTools.IsChannel(source))
            {
                outputRates = new RateControl[] {
                    RateControl.FromSeconds(8, 5),
                    RateControl.FromSeconds(12, 10)
                };
            }
            else
            {
                outputRates = new RateControl[] {
                    RateControl.FromSeconds(30, 20)
                };
            }

            var duration = TimeSpan.FromMinutes(15);

            Triggers = new ThrottleControl(triggerRates, duration);
            Output = new ThrottleControl(outputRates, duration);
        }
    }
}