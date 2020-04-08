using System;
using System.Linq;
using System.Collections.Generic;


namespace MeidoCommon.Parsing
{
    public static class ParseArgs
    {
        // --- Straightforward and not flexible extension methods for getting arguments ---

        public static string ArgString(this ITriggerMsg msg)
        {
            return string.Join(" ", msg.Arguments);
        }

        public static string[] ArgArray(this ITriggerMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            if (msg.Arguments.Count > 0)
            {
                var arguments = new string[msg.Arguments.Count];
                msg.Arguments.CopyTo(arguments, 0);
                return arguments;
            }

            return Array.Empty<string>();
        }

        public static string MessageWithoutTrigger(this ITriggerMsg msg, bool trim = false)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            var message = string.Empty;
            if (msg.MessageParts.Count > 1)
            {
                message =
                    msg.Message
                    .Substring(msg.MessageParts[0].Length + 1);
            }

            if (trim)
                return message.Trim();
            else
                return message;
        }


        // --- LINQy extension methods for getting arguments ---

        // Well, these two are not very LINQy, but sometimes you only care about the arg.
        public static string GetArg(this IIrcMsg msg)
        {
            GetArg(msg, out string arg);
            return arg;
        }

        public static string GetArg(this IEnumerable<string> argv)
        {
            GetArg(argv, out string arg);
            return arg;
        }


        public static IEnumerable<string> GetArg(this IIrcMsg msg,
                                                 out string argument,
                                                 bool toLower = false)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            return GetArg(SkipTrigger(msg), out argument, toLower);
        }

        public static IEnumerable<string> GetArg(this IEnumerable<string> argv,
                                                 out string argument,
                                                 bool toLower = false)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));

            argument = string.Empty;
            int skipCount = 0;

            foreach (var arg in argv)
            {
                skipCount++;
                if (TryGetArg(arg, out argument))
                {
                    if (toLower)
                        argument = argument.ToLowerInvariant();

                    break;
                }
            }

            return argv.Skip(skipCount);
        }


        public static IEnumerable<string> GetEndArg(this IIrcMsg msg,
                                                    out string argument,
                                                    bool toLower = false)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            return GetEndArg(SkipTrigger(msg), out argument, toLower);
        }

        public static IEnumerable<string> GetEndArg(this IEnumerable<string> argv,
                                                    out string argument,
                                                    bool toLower = false)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));

            // Quick and dirty, but I suppose this is good enough for the small
            // sequences we're dealing with.
            return
                argv.Reverse()
                .GetArg(out argument, toLower)
                .Reverse();
        }


        // --- Conventional (not LINQ) extensions methods for getting arguments ---
        // These use ArgEnumerator as backend.

        public static string GetArg(this IIrcMsg msg, out List<string> rest)
        {
            var tmp = GetArgs(msg, 1, out rest);
            return tmp[0];
        }


        public static string[] GetArgs(this IIrcMsg msg, int count)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            return GetArgs(SkipTrigger(msg), count);
        }

        public static string[] GetArgs(this IEnumerable<string> argv, int count)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot be 0 or negative.");

            return InternalGetArgs(argv, 0 - count, out List<string> rest);
        }


        public static string[] GetArgs(this IIrcMsg msg, int count, out List<string> rest)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            return GetArgs(SkipTrigger(msg), count, out rest);
        }

        public static string[] GetArgs(this IEnumerable<string> argv, int count, out List<string> rest)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot be 0 or negative.");

            return InternalGetArgs(argv, count, out rest);
        }


        static string[] InternalGetArgs(IEnumerable<string> argv, int count, out List<string> rest)
        {
            // Negative count signals to not get remaining.
            bool getRest = count > 0;
            var arguments = new string[Math.Abs(count)];
            rest = null;

            using (var argEnum = new ArgEnumerator(argv))
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    // ArgEnumerator already does TryGetArg for us.
                    // It will also populate the array with string.Empty for missing arguments.
                    arguments[i] = argEnum.Next();
                }

                if (getRest)
                    rest = argEnum.GetRemaining();
            }

            return arguments;
        }


        // --- Misc methods ---

        // Method to easily lowercase an entire sequence (of arguments).
        public static IEnumerable<string> ToLowerInvariant(this IEnumerable<string> seq)
        {
            if (seq == null)
                throw new ArgumentNullException(nameof(seq));

            return seq.Select(s => s?.ToLowerInvariant());
        }


        // --- Helper methods for users to test if they got arguments ---

        public static bool Success(params string[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return false;

            foreach (var arg in arguments)
            {
                if (!arg.HasValue())
                    return false;
            }
            return true;
        }

        public static bool HasValue(this string argument)
        {
            return !string.IsNullOrWhiteSpace(argument);
        }


        // --- Helper functions or our own use ---

        // Common, internal helper function such that all argument getting methods get and
        // spit out arguments in the same fashion.
        internal static bool TryGetArg(string possibleArg, out string argument)
        {
            argument = (possibleArg ?? string.Empty).Trim();
            if (argument != string.Empty)
                return true;

            return false;
        }

        internal static IEnumerable<string> SkipTrigger(IIrcMsg msg)
        {
            if (string.IsNullOrEmpty(msg.Trigger))
                return msg.MessageParts;
            else
                return msg.MessageParts.Skip(1);
        }
    }
}