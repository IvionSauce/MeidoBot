using System;

namespace MeidoCommon
{
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
}