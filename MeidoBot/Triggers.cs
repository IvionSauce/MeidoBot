using System;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class Triggers
    {
        public event Action< IEnumerable<Trigger> > PluginTriggersRegister;

        readonly Dictionary<string, Trigger> triggers;

        readonly ThrottleManager throttle;
        readonly Logger log;


        public Triggers(ThrottleManager tManager, Logger log)
        {
            throttle = tManager;
            this.log = log;

            triggers = new Dictionary<string, Trigger>(StringComparer.Ordinal);
        }


        public void RegisterTriggers(IEnumerable<Trigger>[] allTriggers)
        {
            foreach (IEnumerable<Trigger> trigs in allTriggers)
            {
                RegisterTriggers(trigs);
                if (PluginTriggersRegister != null)
                {
                    PluginTriggersRegister(trigs);
                }
            }
        }

        public void RegisterTriggers(IEnumerable<Trigger> triggersPerPlugin)
        {
            foreach (Trigger trig in triggersPerPlugin)
            {
                RegisterTrigger(trig);
            }
        }

        public void RegisterTrigger(Trigger trigger)
        {
            string identifier = trigger.Identifier;
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


        public TriggerThreading GetThreading(string identifier)
        {
            Trigger trigger;
            if (triggers.TryGetValue(identifier, out trigger))
                return trigger.Threading;

            return TriggerThreading.Default;
        }


        public void FireTrigger(IIrcMessage msg)
        {
            Trigger tr;
            if (triggers.TryGetValue(msg.Trigger, out tr))
            {
                string source = msg.Channel ?? "PM";
                log.Message("{0}/{1} {2}", source, msg.Nick, msg.Message);

                if (FirePredicate(msg, tr.Option))
                    tr.Call(msg);
            }
        }

        bool FirePredicate(IIrcMessage msg, TriggerOption opt)
        {
            bool fire = true;
            switch (opt)
            {
                case TriggerOption.ChannelOnly:
                if (msg.Channel == null)
                    fire = false;
                break;

                case TriggerOption.QueryOnly:
                if (msg.Channel != null)
                    fire = false;
                break;
            }

            return fire && !throttle.Triggers(msg);
        }
    }
}