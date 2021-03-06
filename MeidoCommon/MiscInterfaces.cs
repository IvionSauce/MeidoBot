using System;
using System.Collections.Generic;


namespace MeidoCommon
{
    public interface IMeidoHook
    {
        // Things the plugin provides us with.
        string Name { get; }
        string Version { get; }

        // Method to signal to the plugins they need to stop whatever seperate threads they have running.
        // As well as to save/deserialize whatever it needs to.
        void Stop();
    }

    public interface IPluginTriggers
    {
        IEnumerable<Trigger> Triggers { get; }
    }

    public interface IPluginIrcHandlers
    {
        IEnumerable<IIrcHandler> IrcHandlers { get; }
    }

    public interface IPluginHelp
    {
        IEnumerable<TopicHelp> Help { get; }
    }


    public interface ILog
    {
        void Message(string message);
        void Message(string message, params object[] args);
        void Message(IList<string> messages);

        void Verbose(string message);
        void Verbose(string message, params object[] args);
        void Verbose(IList<string> messages);

        void Error(string errorMsg);
        void Error(string errorMsg, params object[] args);
        void Error(Exception ex);
        void Error(string errorMsg, Exception ex);
    }
}