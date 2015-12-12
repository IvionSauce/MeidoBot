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
            // Just log to console for now.
            return Logger.ConsoleLogger(name);
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

        readonly string loggerName;
        readonly Action<string> output;
        readonly Verbosity outputRestraint;


        internal Logger(string name, Action<string> output, Verbosity verb)
        {
            loggerName = name;
            this.output = output;
            outputRestraint = verb;
        }

        public static Logger ConsoleLogger(string name)
        {
            return new Logger(name, Console.WriteLine, Verbosity.Verbose);
        }


        public void Message(string message, params object[] args)
        {
            if (outputRestraint == Verbosity.Normal || outputRestraint == Verbosity.Verbose)
            {
                OutputLog(string.Format(message, args), Verbosity.Normal);
            }
        }

        public void Message(string message)
        {
            if (outputRestraint == Verbosity.Normal || outputRestraint == Verbosity.Verbose)
            {
                OutputLog(message, Verbosity.Normal);
            }
        }

        public void Message(IList<string> messages)
        {
            if (outputRestraint == Verbosity.Normal || outputRestraint == Verbosity.Verbose)
            {
                OutputLog(messages, Verbosity.Normal);
            }
        }


        public void Verbose(string message, params object[] args)
        {
            if (outputRestraint == Verbosity.Verbose)
            {
                OutputLog(string.Format(message, args), Verbosity.Verbose);
            }
        }

        public void Verbose(string message)
        {
            if (outputRestraint == Verbosity.Verbose)
            {
                OutputLog(message, Verbosity.Verbose);
            }
        }

        public void Verbose(IList<string> messages)
        {
            if (outputRestraint == Verbosity.Verbose)
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
            if (outputRestraint == Verbosity.Verbose)
                msg = string.Concat(errorMsg, "\n", ex.ToString());
            else
                msg = string.Concat(errorMsg, " ", ex.Message);

            OutputLog(msg, Verbosity.Errors);
        }


        void OutputLog(string message, Verbosity msgType)
        {
            var logMsg = FormatLogPre(DateTime.Now, msgType) + message;

            output(logMsg);
        }

        void OutputLog(IList<string> messages, Verbosity msgType)
        {
            var pre = FormatLogPre(DateTime.Now, msgType);
            foreach (string msg in messages)
            {
                var logMsg = pre + msg;
                output(logMsg);
            }
        }


        string FormatLogPre(DateTime dt, Verbosity msgType)
        {
            var time = dt.ToString("s");

            string prefix;
            switch (msgType)
            {
            case Verbosity.Normal:
            case Verbosity.Verbose:
                prefix = string.Format("{0} {1}[{2}] ",
                    time, loggerName, Thread.CurrentThread.ManagedThreadId);
                break;
            case Verbosity.Errors:
                prefix = string.Format("!! {0} {1}[{2}] ",
                    time, loggerName, Thread.CurrentThread.ManagedThreadId);
                break;
            default:
                throw new InvalidOperationException("Unexpected Message Type Verbosity.");
            }
            return prefix;
        }
    }

}