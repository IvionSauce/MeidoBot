using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MeidoCommon;


namespace MeidoBot
{
    class Triggers
    {
        public readonly string Prefix;

        readonly List<string> primeIds;
        public readonly ReadOnlyCollection<string> PrimeIdentifiers;


        readonly Dictionary<string, Trigger> triggers;
        readonly ThrottleManager throttle;
        readonly Logger log;


        public Triggers(string triggerPrefix, ThrottleManager tManager, Logger log)
        {
            Prefix = triggerPrefix;
            primeIds = new List<string>();
            PrimeIdentifiers = new ReadOnlyCollection<string>(primeIds);

            throttle = tManager;
            this.log = log;
            triggers = new Dictionary<string, Trigger>(StringComparer.Ordinal);
        }


        public bool AddTrigger(Trigger trigger, MeidoPlugin plugin)
        {
            bool success = false;
            // Single trigger, but with possible multiple identifiers.
            foreach (var id in trigger.Identifiers)
            {
                // We consider it a success if even one identifier is successfully registered.
                if (RegisterTrigger(id, trigger, plugin.Name))
                {
                    // Regard the first successful registered id of a trigger as the prime id.
                    if (!success)
                        primeIds.Add(id);

                    success = true;
                }
            }

            return success;
        }

        bool RegisterTrigger(string identifier, Trigger tr, string pluginName)
        {
            switch (identifier)
            {
                case "h":
                case "help":
                case "auth":
                case "admin":
                log.Error("{0}: Tried to register reserved trigger '{1}'",
                          pluginName, identifier);
                return false;
            }

            if (!triggers.ContainsKey(identifier))
            {
                log.Verbose("{0}: Registering '{1}'", pluginName, identifier);
                triggers[identifier] = tr;
                return true;
            }
            else
            {
                log.Error("{0}: Tried to register previously registered trigger '{1}'",
                          pluginName, identifier);
                return false;
            }
        }

        public void SpecialTrigger(string identifier, Action<ITriggerMsg> callback)
        {
            log.Verbose("Meido: Registering '{0}'", identifier);
            triggers[identifier] = new Trigger(identifier, callback);
        }


        public bool TryGet(string identifier, out Trigger tr)
        {
            return triggers.TryGetValue(identifier, out tr);
        }


        public IEnumerable<string> PrimeIds(IEnumerable<Trigger> trigs)
        {
            return
                from tr in trigs
                select GetPrimeId(tr) into prime
                where prime != null
                select prime;
        }

        // Plug trigger identifiers back into registered id's and return the first id
        // that points to the trigger.
        string GetPrimeId(Trigger tr)
        {
            return tr.Identifiers
                     .FirstOrDefault(id => IsRegisteredAs(tr, id));
        }

        // Check if possible prime identifier /actually/ points to the trigger,
        // another trigger might've snatched the identifier.
        public bool IsRegisteredAs(Trigger tr, string identifier)
        {
            Trigger registeredTrig;
            return TryGet(identifier, out registeredTrig) &&
                registeredTrig == tr;
        }


        public Action Delegate(Trigger tr, ITriggerMsg msg)
        {
            return () => Fire(tr, msg);
        }

        public void Fire(Trigger tr, ITriggerMsg msg)
        {
            string source = msg.Nick;
            if (msg.Channel != null)
                source += "/" + msg.Channel;
            
            log.Message("{0} {1}", source, msg.Message);

            if (FirePredicate(msg, tr.Option))
                tr.Call(msg);
        }

        bool FirePredicate(ITriggerMsg msg, TriggerOption opt)
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