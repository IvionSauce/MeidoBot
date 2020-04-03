﻿using System;
using System.Linq;
using System.Collections.Generic;


namespace MeidoCommon.Parsing
{
    public static class ParseArgs
    {
        public static string ArgString(this IIrcMsg msg)
        {
            return string.Join(" ", ArgArray(msg));
        }

        public static string[] ArgArray(this IIrcMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            string[] arguments = null;
            var argv = msg.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            if (argv.Length > 1)
            {
                arguments = new string[argv.Length - 1];
                Array.Copy(argv, 1, arguments, 0, arguments.Length);
            }

            return arguments ?? new string[0];
        }

        public static string MessageWithoutTrigger(this IIrcMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));
            if (string.IsNullOrEmpty(msg.Trigger))
                throw new ArgumentException("Message doesn't have a trigger.");

            return msg.Message.Substring(msg.MessageArray[0].Length + 1);
        }


        public static IEnumerable<string> GetArg(this IIrcMsg msg, out string argument)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            return GetArg(msg.MessageArray.Skip(1), out argument);
        }

        static IEnumerable<string> GetArg(this IEnumerable<string> argv, out string argument)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));

            argument = string.Empty;
            int skipCount = 0;

            foreach (var arg in argv)
            {
                skipCount++;
                if (TryGetArg(arg, out argument))
                    break;
            }

            return argv.Skip(skipCount);
        }


        public static string GetArg(this IIrcMsg msg, out List<string> rest)
        {
            var tmp = GetArgs(msg, 1, out rest);
            if (tmp != null)
                return tmp[0];
            else
                return string.Empty;
        }

        public static string[] GetArgs(this IIrcMsg msg, int count)
        {
            return GetArgs(msg, count, out List<string> rest);
        }

        public static string[] GetArgs(this IIrcMsg msg, int count, out List<string> rest)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            return GetArgs(msg.MessageArray.Skip(1), count, out rest);
        }

        static string[] GetArgs(this IEnumerable<string> argv, int count, out List<string> rest)
        {
            if (argv == null)
                throw new ArgumentNullException(nameof(argv));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot be 0 or negative.");

            var arguments = new string[count];
            rest = null;

            using (var argEnum = new ArgEnumerator(argv))
            {
                int idx = 0;

                while (idx < count && argEnum.NextArg())
                {
                    arguments[idx] = argEnum.CurrentArg;
                }

                if (idx == (count - 1))
                    rest = argEnum.GetRemaining();
                else
                    arguments = null;
            }

            return arguments;
        }


        public static bool Success(params string[] arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            foreach (var arg in arguments)
            {
                if (!arg.HasValue())
                    return false;
            }
            return true;
        }

        public static bool HasValue(this string arg)
        {
            return !string.IsNullOrWhiteSpace(arg);
        }


        internal static bool TryGetArg(string possibleArg, out string argument)
        {
            argument = (possibleArg ?? string.Empty).Trim();
            if (argument != string.Empty)
                return true;

            return false;
        }
    }
}