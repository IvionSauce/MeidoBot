using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace MeidoCommon
{
    public class TriggerHelp : CommandBaseHelp, IHelpNode
    {
        public readonly ReadOnlyCollection<CommandHelp> Commands;
        public Trigger ParentTrigger { get; internal set; }

        // IHelpNode properties.
        public IHelpNode Parent
        {
            // We don't have a parent, TriggerHelp is the root of the help tree.
            get { return null; }
        }

        public IEnumerable<IHelpNode> Siblings
        {
            // We can have siblings though, kinda. One can group triggers by setting RelatedTriggers.
            get
            {
                if (ParentTrigger != null)
                {
                    return from tr in ParentTrigger.RelatedTriggers
                           where tr.Help != null
                           select tr.Help;
                }
                return new IHelpNode[0];
            }
        }

        public IEnumerable<IHelpNode> Children
        {
            get { return Commands; }
        }


        // Shared field initialization.
        TriggerHelp(
            string parameters,
            DynamicHelp dHelp,
            bool initCommands) : base(dHelp, parameters)
        {
            if (initCommands)
                Commands = new ReadOnlyCollection<CommandHelp>(new CommandHelp[0]);
        }

        // Triggers with no command subdivision.
        TriggerHelp(string parameters, DynamicHelp dHelp)
            : this(parameters, dHelp, true) {}

        // Triggers with various commands.
        TriggerHelp(
            string parameters,
            DynamicHelp dHelp,
            CommandHelp[] commands) : this(parameters, dHelp, false)
        {
            Commands = Tools.ProcessCommands(commands, nameof(commands));
        }


        // --- Documentation as string overloads ---

        public TriggerHelp(string documentation)
            : this(null, new DynamicHelp(documentation)) {}

        public TriggerHelp(string parameters, string documentation)
            : this(parameters, new DynamicHelp(documentation)) {}

        public TriggerHelp(string documentation, params CommandHelp[] commands)
            : this(null, new DynamicHelp(documentation), commands) {}

        public TriggerHelp(
            string parameters,
            string documentation,
            params CommandHelp[] commands)
            : this(parameters, new DynamicHelp(documentation), commands) {}


        // --- Documentation as IEnumerable<string> overloads ---

        public TriggerHelp(IEnumerable<string> documentation)
            : this(null, new DynamicHelp(documentation)) {}

        public TriggerHelp(string parameters, IEnumerable<string> documentation)
            : this(parameters, new DynamicHelp(documentation)) {}

        public TriggerHelp(IEnumerable<string> documentation, params CommandHelp[] commands)
            : this(null, new DynamicHelp(documentation), commands) {}

        public TriggerHelp(
            string parameters,
            IEnumerable<string> documentation,
            params CommandHelp[] commands)
            : this(parameters, new DynamicHelp(documentation), commands) {}


        // --- Documentation as Func<string> overloads ---

        public TriggerHelp(Func<string> documentation)
            : this(null, new DynamicHelp(documentation)) {}

        public TriggerHelp(string parameters, Func<string> documentation)
            : this(parameters, new DynamicHelp(documentation)) {}

        public TriggerHelp(Func<string> documentation, params CommandHelp[] commands)
            : this(null, new DynamicHelp(documentation), commands) {}

        public TriggerHelp(
            string parameters,
            Func<string> documentation,
            params CommandHelp[] commands)
            : this(parameters, new DynamicHelp(documentation), commands) {}
    }
}