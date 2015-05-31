using System;
using System.Threading;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class LogFactory
    {
        public string Server { get; private set; }
        public Logger.Verbosity Verbosity { get; set; }


        public LogFactory (string server)
        {
            Server = server;
            Verbosity = Logger.Verbosity.Verbose;
        }


        public Logger CreateLogger(IMeidoHook plugin)
        {
            string name;
            switch (plugin.Name)
            {
            case "":
            case null:
                name = "Unknown";
                break;
            case "MEIDO":
            case "AUTH":
                name = "_" + plugin.Name;
                break;
            default:
                name = plugin.Name;
                break;
            }

            return CreateLogger(name);
        }

        public Logger CreateLogger(string name)
        {
            return new Logger(Server, name, Verbosity);
        }
    }


    class Logger : ILog
    {
        public enum Verbosity
        {
            Normal,
            Verbose,
            Errors
        }

        readonly string msgPrefix;
        readonly Verbosity verbosity;


        internal Logger(string server, string name, Verbosity verb)
        {
            msgPrefix = string.Format("[{0}] {1}", server, name);
            verbosity = verb;
        }


        public void Message(string message, params object[] args)
        {
            if (verbosity == Verbosity.Normal || verbosity == Verbosity.Verbose)
            {
                OutputLog(string.Format(message, args), Verbosity.Normal);
            }
        }

        public void Message(string message)
        {
            if (verbosity == Verbosity.Normal || verbosity == Verbosity.Verbose)
            {
                OutputLog(message, Verbosity.Normal);
            }
        }

        public void Message(IList<string> messages)
        {
            if (verbosity == Verbosity.Normal || verbosity == Verbosity.Verbose)
            {
                OutputLog(messages, Verbosity.Normal);
            }
        }


        public void Verbose(string message, params object[] args)
        {
            if (verbosity == Verbosity.Verbose)
            {
                OutputLog(string.Format(message, args), Verbosity.Verbose);
            }
        }

        public void Verbose(string message)
        {
            if (verbosity == Verbosity.Verbose)
            {
                OutputLog(message, Verbosity.Verbose);
            }
        }

        public void Verbose(IList<string> messages)
        {
            if (verbosity == Verbosity.Verbose)
            {
                OutputLog(messages, Verbosity.Verbose);
            }
        }


        public void Error(string errorMsg, params object[] args)
        {
            OutputLog(string.Format(errorMsg, args), Verbosity.Errors);
        }

        public void Error(string errorMsg)
        {
            OutputLog(errorMsg, Verbosity.Errors);
        }

        public void Error(Exception ex)
        {
            Error("An exception occurred:", ex);
        }

        public void Error(string errorMsg, Exception ex)
        {
            string msg;
            if (verbosity == Verbosity.Verbose)
                msg = string.Concat(errorMsg, "\n", ex.ToString());
            else
                msg = string.Concat(errorMsg, " ", ex.Message);

            OutputLog(msg, Verbosity.Errors);
        }


        void OutputLog(string message, Verbosity verb)
        {
            var logMsg = FormatLogEntry(DateTime.Now, verb) + message;

            Console.WriteLine(logMsg);
        }

        void OutputLog(IList<string> messages, Verbosity verb)
        {
            var pre = FormatLogEntry(DateTime.Now, verb);
            foreach(string msg in messages)
            {
                var logMsg = pre + msg;
                Console.WriteLine(logMsg);
            }
        }


        string FormatLogEntry(DateTime dt, Verbosity verb)
        {
            var time = dt.ToString("s");

            string prefix;
            switch(verb)
            {
            case Verbosity.Normal:
            case Verbosity.Verbose:
                prefix = string.Format("{0} {1}[{2}] ",
                                       time, msgPrefix, Thread.CurrentThread.ManagedThreadId);
                break;
            case Verbosity.Errors:
                prefix = string.Format("!! {0} {1}[{2}] ",
                                       time, msgPrefix, Thread.CurrentThread.ManagedThreadId);
                break;
            default:
                throw new InvalidOperationException("Unexpected Verbosity.");
            }
            return prefix;
        }
    }

}