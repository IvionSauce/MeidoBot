using System;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class Triggers
    {
        readonly Dictionary<string, Trigger> triggers = new Dictionary<string, Trigger>(StringComparer.Ordinal);

        readonly ThrottleManager throttle;
        readonly Logger log;


        public Triggers(ThrottleManager tManager, Logger log)
        {
            throttle = tManager;
            this.log = log;
        }


        public void RegisterTrigger(string identifier, Trigger trigger)
        {
            log.Verbose("Registering trigger '{0}'.", identifier);
            triggers[identifier] = trigger;
        }

        public void FireTrigger(IrcMessage msg)
        {
            Trigger tr;
            if (triggers.TryGetValue(msg.Trigger, out tr))
            {
                string source = msg.Channel ?? "PM";
                log.Message("{0}/{1} {2}", source, msg.Nick, msg.Message);

                if (FirePredicate(msg, tr))
                    tr.Call(msg);
            }
        }

        bool FirePredicate(IrcMessage msg, Trigger trigger)
        {
            // First clause: make sure the trigger only gets called in a context it supports (whether or not there's
            // a channel property).
            // Second clause: whether or not triggers are being throttled.
            if (!(trigger.NeedsChannel && msg.Channel == null) &&
                !throttle.Triggers(msg))
            {
                return true;
            }
            else
                return false;
        }
    }


    class Trigger
    {
        public readonly Action<IIrcMessage> Call;
        public readonly bool NeedsChannel;

        public Trigger(Action<IIrcMessage> call, bool needChannel)
        {
            Call = call;
            NeedsChannel = needChannel;
        }
    }
}