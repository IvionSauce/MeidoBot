using System;
using System.Collections.Generic;
using MeidoCommon.ExtensionMethods;


namespace MeidoCommon
{
    // --- Interfaces ---

    // Uniform travelling when dealing with Trigger- and CommandHelp.
    public interface IHelpNode
    {
        IHelpNode Parent { get; }
        IEnumerable<IHelpNode> Siblings { get; }
        IEnumerable<IHelpNode> Children { get; }
    }


    // --- Abstract base classes ---
    // Not pretty, but it does allow some degree of abstraction over common properties.
    // These probably also could've been interfaces, but I didn't want to implement them
    // on all the subclasses - hence regular inheritance proved to be the most expedient.

    public abstract class BaseHelp
    {
        public readonly IEnumerable<string> Documentation;
        IEnumerable<TopicHelp> _alsoSee;
        public IEnumerable<TopicHelp> AlsoSee
        {
            get { return _alsoSee.NoNull(); }
            set { _alsoSee = value; }
        }


        internal BaseHelp(DynamicHelp dHelp)
        {
            Documentation = dHelp.Documentation;
        }

        // Cloning constructor.
        internal BaseHelp(BaseHelp help)
        {
            Documentation = help.Documentation;
            AlsoSee = help.AlsoSee;
        }
    }

    public abstract class CommandBaseHelp : BaseHelp
    {
        /* We'd also like to abstract over some kind of identifier, but TriggerHelp
         * and CommandHelp are quite different in that regard. TriggerHelp can have
         * multiple identifiers, while CommandHelp has always one. Maybe in the future
         * we'll find a way to unify them.
        */
        public readonly string Parameters;


        internal CommandBaseHelp(DynamicHelp dHelp, string parameters) : base(dHelp)
        {
            Parameters = parameters;
        }

        // Cloning constructor
        internal CommandBaseHelp(CommandBaseHelp help) : base(help)
        {
            Parameters = help.Parameters;
        }
    }
}