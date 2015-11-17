using System;

namespace MeidoCommon
{
    public interface IMeidoComm
    {
        string ConfDir { get; }
        string DataDir { get; }

        ILog CreateLogger(IMeidoHook plugin);

        void RegisterTrigger(string trigger, Action<IIrcMessage> callback);
        void RegisterTrigger(string trigger, Action<IIrcMessage> callback, bool needChannel);

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