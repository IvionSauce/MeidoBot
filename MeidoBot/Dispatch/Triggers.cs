﻿using System;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class Triggers
    {
        readonly Dictionary<string, Trigger> triggers;

        readonly ThrottleManager throttle;
        readonly Logger log;


        public Triggers(ThrottleManager tManager, Logger log)
        {
            throttle = tManager;
            this.log = log;

            triggers = new Dictionary<string, Trigger>(StringComparer.Ordinal);
        }


        public void RegisterTriggers(MeidoPlugin plugin)
        {
            // Triggers of a single plugin.
            foreach (var trig in plugin.Triggers)
            {
                // Single trigger, but with possible multiple identifiers.
                foreach (var id in trig.Identifiers)
                {
                    RegisterTrigger(id, trig, plugin.Name);
                }
            }
        }

        void RegisterTrigger(string identifier, Trigger tr, string pluginName)
        {
            switch (identifier)
            {
                case "h":
                case "help":
                case "auth":
                case "admin":
                log.Error("{0}: Tried to register reserved trigger '{1}'",
                          pluginName, identifier);

                return;
            }

            if (!triggers.ContainsKey(identifier))
            {
                log.Verbose("{0}: Registering '{1}'", pluginName, identifier);
                triggers[identifier] = tr;
            }
            else
                log.Error("{0}: Tried to register previously registered trigger '{1}'",
                          pluginName, identifier);
        }

        public void SpecialTrigger(string identifier, Action<IIrcMessage> callback)
        {
            log.Verbose("Meido: Registering '{0}'", identifier);
            triggers[identifier] = new Trigger(identifier, callback);
        }


        public bool TryGet(string identifier, out Trigger tr)
        {
            return triggers.TryGetValue(identifier, out tr);
        }


        public Action Delegate(Trigger tr, IIrcMessage msg)
        {
            return () => Fire(tr, msg);
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