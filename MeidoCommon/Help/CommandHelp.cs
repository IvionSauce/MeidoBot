using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace MeidoCommon
{
    public class CommandHelp : CommandBaseHelp, IHelpNode
    {
        public readonly string Command;
        public readonly ReadOnlyCollection<CommandHelp> Subcommands;

        // IHelpNode properties.
        public IHelpNode Parent { get; private set; }

        public IEnumerable<IHelpNode> Siblings
        {
            get
            {
                if (Parent != null)
                    return Parent.Children.Where(node => node != this);

                return new IHelpNode[0];
            }
        }

        public IEnumerable<IHelpNode> Children
        {
            get { return Subcommands; }
        }


        // Shared argument checking and field initialization.
        CommandHelp(
            string command,
            string parameters,
            DynamicHelp dHelp,
            bool initSubcommands) : base(dHelp, parameters)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (command == string.Empty)
                throw new ArgumentException("Cannot be empty string.", nameof(command));
            if (Tools.ContainsWhitespace(command))
                throw new ArgumentException("Cannot contain whitespace.", nameof(command));

            Command = command;
            if (initSubcommands)
                Subcommands = new ReadOnlyCollection<CommandHelp>(new CommandHelp[0]);
        }

        // Commands with no subcommands.
        CommandHelp(
            string command,
            string parameters,
            DynamicHelp dHelp) : this(command, parameters, dHelp, true) {}

        // Commands with subcommands.
        CommandHelp(
            string command,
            string parameters,
            DynamicHelp dHelp,
            CommandHelp[] subcommands) : this(command, parameters, dHelp, false)
        {
            Subcommands = Tools.ProcessCommands(subcommands, nameof(subcommands));
        }


        // Minimal constructor for interstitial commands, ie commands that don't do anything
        // themselves (thus require no parameters or documentation) but serve to group subcommands.
        public CommandHelp(string command, params CommandHelp[] subcommands)
            : this(command, null, new DynamicHelp(), subcommands) {}


        // --- Documentation as string overloads ---

        public CommandHelp(string command, string documentation)
            : this(command, null, new DynamicHelp(documentation)) {}

        public CommandHelp(
            string command,
            string parameters,
            string documentation)
            : this(command, parameters, new DynamicHelp(documentation)) {}

        public CommandHelp(
            string command,
            string documentation,
            params CommandHelp[] subcommands)
            : this(command, null, new DynamicHelp(documentation), subcommands) {}

        public CommandHelp(
            string command,
            string parameters,
            string documentation,
            params CommandHelp[] subcommands)
            : this(command, parameters, new DynamicHelp(documentation), subcommands) {}


        // --- Documentation as IEnumerable<string> overloads ---

        public CommandHelp(string command, IEnumerable<string> documentation)
            : this(command, null, new DynamicHelp(documentation)) {}

        public CommandHelp(
            string command,
            string parameters,
            IEnumerable<string> documentation)
            : this(command, parameters, new DynamicHelp(documentation)) {}

        public CommandHelp(
            string command,
            IEnumerable<string> documentation,
            params CommandHelp[] subcommands)
            : this(command, null, new DynamicHelp(documentation), subcommands) {}

        public CommandHelp(
            string command,
            string parameters,
            IEnumerable<string> documentation,
            params CommandHelp[] subcommands)
            : this(command, parameters, new DynamicHelp(documentation), subcommands) {}


        // --- Documentation as Func<string> overloads ---

        public CommandHelp(string command, Func<string> documentation)
            : this(command, null, new DynamicHelp(documentation)) {}

        public CommandHelp(
            string command,
            string parameters,
            Func<string> documentation)
            : this(command, parameters, new DynamicHelp(documentation)) {}

        public CommandHelp(
            string command,
            Func<string> documentation,
            params CommandHelp[] subcommands)
            : this(command, null, new DynamicHelp(documentation), subcommands) {}

        public CommandHelp(
            string command,
            string parameters,
            Func<string> documentation,
            params CommandHelp[] subcommands)
            : this(command, parameters, new DynamicHelp(documentation), subcommands) {}


        // Cloning constructor.
        public CommandHelp(CommandHelp help) : base(help)
        {
            if (help == null)
                throw new ArgumentNullException(nameof(help));

            // These are all read-only and can just be copied.
            // (This also goes for the properties copied in the base class)
            Command = help.Command;
            Subcommands = help.Subcommands;
            // This one isn't, but we strictly control writing to it.
            // Namely we only change it _when cloning_.
            Parent = help.Parent;
        }

        public CommandHelp Clone()
        {
            return new CommandHelp(this);
        }

        // Clones and re-parents the clone.
        public CommandHelp Clone(IHelpNode parent)
        {
            var help = Clone();
            help.Parent = parent;
            return help;
        }
    }
}