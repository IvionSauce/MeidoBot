using System;
using System.Collections.Generic;

namespace MeidoCommon
{
    public interface IMeidoHook
    {
        string Name { get; }
        string Version { get; }
        Dictionary<string, string> Help { get; }

        string Prefix { set; }
    }

    public interface IIrcMessage
    {
        string Message { get; }
        string[] MessageArray { get; }
        string Channel { get; }
        string Nick { get; }
        string Ident { get; }
        string Host { get; }
    }

    public interface IMeidoComm
    {}

    public interface IIrcComm
    {
        void AddChannelMessageHandler(Action<IIrcMessage> handler);
        void AddQueryMessageHandler(Action<IIrcMessage> handler);

        void SendMessage(string target, string message);
        void DoAction(string target, string action);
        void SendNotice(string target, string message);

        string[] GetChannels();
    }
}