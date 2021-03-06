﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MeidoCommon.ExtensionMethods;


namespace MeidoCommon
{
    public enum TriggerThreading
    {
        Default,
        Queue,
        Threadpool
    }

    public enum TriggerOption
    {
        None,
        ChannelOnly,
        QueryOnly
    }


    public class Trigger
    {
        public readonly ReadOnlyCollection<string> Identifiers;
        public readonly Action<ITriggerMsg> Call;
        public readonly TriggerOption Option;
        public readonly TriggerThreading Threading;

        IEnumerable<Trigger> _related;
        public IEnumerable<Trigger> RelatedTriggers
        {
            get
            {
                return _related.NoNull()
                               .Where(tr => tr != this);
            }
            set { _related = value; }
        }

        TriggerHelp _help;
        public TriggerHelp Help
        {
            get { return _help; }
            set
            {
                _help = value;
                if (_help != null)
                    _help.ParentTrigger = this;
            }
        }


        const string nullEx = "Trigger identifiers cannot be null.";
        const string emptyEx = "Trigger identifiers cannot be empty string.";
        const string whiteEx = "Trigger identifiers cannot contain whitespace characters.";


        // --- Single identifier constructors ---

        public Trigger(string identifier, Action<ITriggerMsg> call) :
        this(identifier, call, TriggerOption.None, TriggerThreading.Default) {}

        public Trigger(string identifier, Action<ITriggerMsg> call, TriggerOption opt) :
        this(identifier, call, opt, TriggerThreading.Default) {}

        public Trigger(string identifier, Action<ITriggerMsg> call, TriggerThreading threading) :
        this(identifier, call, TriggerOption.None, threading) {}

        public Trigger(
            string identifier,
            Action<ITriggerMsg> call,
            TriggerOption opt,
            TriggerThreading threading)
        {
            if (identifier == null)
                throw new ArgumentNullException(nameof(identifier), nullEx);
            if (call == null)
                throw new ArgumentNullException(nameof(call));
            if (identifier == string.Empty)
                throw new ArgumentException(emptyEx, nameof(identifier));
            if (Tools.ContainsWhitespace(identifier))
                throw new ArgumentException(whiteEx, nameof(identifier));

            Identifiers = new ReadOnlyCollection<string>(new[] {identifier});
            Call = call;
            Option = opt;
            Threading = threading;
        }


        // --- Multiple identifiers constructors ---

        public Trigger(Action<ITriggerMsg> call, params string[] identifiers) :
        this(call, TriggerOption.None, TriggerThreading.Default, identifiers) {}

        public Trigger(Action<ITriggerMsg> call, TriggerOption opt, params string[] identifiers) :
        this(call, opt, TriggerThreading.Default, identifiers) {}

        public Trigger(Action<ITriggerMsg> call, TriggerThreading threading, params string[] identifiers) :
        this(call, TriggerOption.None, threading, identifiers) {}

        public Trigger(
            Action<ITriggerMsg> call,
            TriggerOption opt,
            TriggerThreading threading,
            params string[] identifiers)
        {
            if (call == null)
                throw new ArgumentNullException(nameof(call));
            if (identifiers == null)
                throw new ArgumentNullException(nameof(identifiers));
            if (identifiers.Length == 0)
                throw new ArgumentException("Cannot be an empty collection.", nameof(identifiers));

            // Create our own copy.
            var triggerIdents = new string[identifiers.Length];

            for (int i = 0; i < identifiers.Length; i++)
            {
                var id = identifiers[i];

                if (id == null)
                    throw new ArgumentException(nullEx, nameof(identifiers));
                if (id == string.Empty)
                    throw new ArgumentException(emptyEx, nameof(identifiers));
                if (Tools.ContainsWhitespace(id))
                    throw new ArgumentException(whiteEx, nameof(identifiers));

                // We need to check each element anyway, so use the loop the create our own copy
                // instead of using Array.Copy afterwards.
                triggerIdents[i] = id;
            }

            Identifiers = new ReadOnlyCollection<string>(triggerIdents);
            Call = call;
            Option = opt;
            Threading = threading;
        }


        // Modifies `triggers` in place, but also returns it. It would be clearer
        // if we returned void, but this way makes it nice to use for initialization.
        public static Trigger[] Group(params Trigger[] triggers)
        {
            if (triggers == null)
                throw new ArgumentNullException(nameof(triggers));
            
            foreach (Trigger tr in triggers)
            {
                if (tr != null)
                    tr.RelatedTriggers = triggers;
            }

            return triggers;
        }
    }


    public static class TriggerExtensions
    {
        // Extension method to make initializing multiple groups really nice.
        // Because of the call to Trigger.Group this modifies `adds` in place.
        public static Trigger[] AddGroup(this Trigger[] triggers, params Trigger[] adds)
        {
            if (triggers == null)
                throw new ArgumentNullException(nameof(triggers));
            if (adds == null)
                throw new ArgumentNullException(nameof(adds));
            
            Trigger.Group(adds);
            var enlarged = new Trigger[triggers.Length + adds.Length];
            Array.Copy(triggers, enlarged, triggers.Length);
            Array.Copy(adds, 0, enlarged, triggers.Length, adds.Length);

            return enlarged;
        }
    }
}