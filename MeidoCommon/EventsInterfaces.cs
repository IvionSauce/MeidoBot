using System;
using System.Collections.ObjectModel;

namespace MeidoCommon
{
    public interface ITriggerMsg : IIrcMsg
    {}

    public interface IChannelAction : IIrcMsg
    {}
    public interface IQueryAction : IIrcMsg
    {}

    public interface IChannelMsg : IIrcMsg
    {}
    public interface IQueryMsg : IIrcMsg
    {}

    public interface IIrcMsg : IIrcHandlerEvent
    {
        IIrcComm Irc { get; }

        string Message { get; }
        [Obsolete("Use MessageParts or the MeidoCommon.Parsing extension methods instead")]
        string[] MessageArray { get; }
        ReadOnlyCollection<string> MessageParts { get; }

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

    public interface IIrcHandlerEvent
    {}
}