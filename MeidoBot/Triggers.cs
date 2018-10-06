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


        public void RegisterTriggers(PluginTriggers[] allTriggers)
        {
            foreach (var trigs in allTriggers)
            {
                RegisterTriggers(trigs);
                if (PluginTriggersRegister != null)
                {
                    PluginTriggersRegister(trigs.Triggers);
                }
            }
        }

        public void RegisterTriggers(PluginTriggers plugin)
        {
            foreach (var trig in plugin.Triggers)
            {
                RegisterTrigger(trig, plugin.Name);
            }
        }

        public void RegisterTrigger(Trigger tr, string pluginName)
        {
            switch (tr.Identifier)
            {
                case "h":
                case "help":
                case "auth":
                case "admin":
                log.Error(
                    "{0}: Tried to register reserved trigger '{1}', this is not allowed.",
                    pluginName, tr.Identifier);
                
                return;
            }

            log.Verbose("{0}: Trigger '{1}'.", pluginName, tr.Identifier);
            triggers[tr.Identifier] = tr;
        }

        public void SpecialTrigger(string identifier, Action<IIrcMessage> callback)
        {
            triggers[identifier] = new Trigger(identifier, callback);
        }


        public bool TryGet(string identifier, out Trigger tr)
        {
            return triggers.TryGetValue(identifier, out tr);
        }

        public Action<IIrcMessage> Delegate(Trigger tr)
        {
            return (msg) => Fire(tr, msg);
        }

        public void Fire(Trigger tr, IIrcMessage msg)
        {
            string source = msg.Nick;
            if (msg.Channel != null)
                source += "/" + msg.Channel;
            
            log.Message("{0} {1}", source, msg.Message);

            if (FirePredicate(msg, tr.Option))
                tr.Call(msg);
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