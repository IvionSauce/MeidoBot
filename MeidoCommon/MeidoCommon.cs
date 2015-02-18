using System;
using System.Collections.Generic;

namespace MeidoCommon
{
    public interface IMeidoHook
    {
        // Things the plugin provides us with.
        string Name { get; }
        string Version { get; }
        Dictionary<string, string> Help { get; }

        // Things we provide to the plugin.
        string Prefix { set; }

        // Method to signal to the plugins they need to stop whatever seperate threads they have running.
        // As well as to save/deserialize whatever it needs to.
        void Stop();
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


    public interface IIrcMessage
    {
        string Message { get; }
        string[] MessageArray { get; }
        string Channel { get; }
        string Nick { get; }
        string Ident { get; }
        string Host { get; }

        string Trigger { get; }
        string ReturnTo { get; }

        void Reply(string message);
        void Reply(string message, params object[] args);
        void SendNotice(string message);
        void SendNotice(string message, params object[] args);
    }


    public interface IMeidoComm
    {
        string ConfDir { get; }
        string DataDir { get; }

        ILog CreateLogger(IMeidoHook plugin);
        bool Auth(string nick, string pass);
        int AuthLevel(string nick);
    }


    public interface IIrcComm
    {
        string Nickname { get; }

        void AddChannelMessageHandler(Action<IIrcMessage> handler);
        void AddChannelActionHandler(Action<IIrcMessage> handler);

        void AddQueryMessageHandler(Action<IIrcMessage> handler);
        void AddQueryActionHandler(Action<IIrcMessage> handler);

        void AddTriggerHandler(Action<IIrcMessage> handler);

        void SendMessage(string target, string message);
        void SendMessage(string target, string message, params object[] args);

        void DoAction(string target, string action);
        void DoAction(string target, string action, params object[] args);

        void SendNotice(string target, string message);
        void SendNotice(string target, string message, params object[] args);

        string[] GetChannels();
        bool IsMe(string nick);
        bool IsJoined(string channel, string nick);
    }
}