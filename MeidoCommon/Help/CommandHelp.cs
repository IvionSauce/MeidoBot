using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace MeidoCommon
{
    public class CommandHelp : BaseHelp
    {
        public readonly string Command;
        public readonly string Parameters;
        public readonly ReadOnlyCollection<CommandHelp> Subcommands;


        // Shared argument checking and field initialization.
        CommandHelp(
            string command,
            string parameters,
            DynamicHelp dHelp,
            bool initSubcommands) : base(dHelp)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (command == string.Empty)
                throw new ArgumentException("Cannot be empty string.", nameof(command));
            if (Tools.ContainsWhitespace(command))
                throw new ArgumentException("Cannot contain whitespace.", nameof(command));

            Command = command;
            Parameters = parameters ?? string.Empty;
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
    }
}