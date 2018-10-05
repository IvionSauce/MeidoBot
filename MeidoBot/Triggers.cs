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
            switch (identifier)
            {
                case "h":
                case "help":
                case "auth":
                case "admin":
                log.Error(
                    "A plugin tried to register reserved trigger '{0}', this is not allowed.",
                    identifier);
                return;
            }

            log.Verbose("Registering trigger '{0}'.", identifier);
            triggers[identifier] = trigger;
        }

        public void SpecialTrigger(string identifier, Action<IIrcMessage> callback)
        {
            triggers[identifier] = new Trigger(identifier, callback);
        }


        public void FireTrigger(IIrcMessage msg)
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

        bool FirePredicate(IIrcMessage msg, Trigger trigger)
        {
            bool fire = true;
            switch (trigger.Options)
            {
                case TriggerOptions.ChannelOnly:
                if (msg.Channel == null)
                    fire = false;
                break;

                case TriggerOptions.QueryOnly:
                if (msg.Channel != null)
                    fire = false;
                break;
            }

            return fire && !throttle.Triggers(msg);
        }
    }
}