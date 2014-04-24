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

    public interface IIrcMessage
    {
        string Message { get; }
        string[] MessageArray { get; }
        string Channel { get; }
        string Nick { get; }
        string Ident { get; }
        string Host { get; }

        void Reply(string message);
        void Reply(string message, params object[] args);
    }

    public interface IMeidoComm
    {
        string ConfDir { get; }
    }

    public interface IIrcComm
    {
        void AddChannelMessageHandler(Action<IIrcMessage> handler);
        void AddQueryMessageHandler(Action<IIrcMessage> handler);

        void SendMessage(string target, string message);
        void SendMessage(string target, string message, params object[] args);

        void DoAction(string target, string action);
        void DoAction(string target, string action, params object[] args);

        void SendNotice(string target, string message);
        void SendNotice(string target, string message, params object[] args);

        string[] GetChannels();
        bool IsMe(string nick);
    }
}