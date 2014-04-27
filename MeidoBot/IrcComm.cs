using System;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    // Implement IIrcComm by using a IrcClient as backend, allowing a limited subset of its methods to be used by
    // the plugins.
    class IrcComm : IIrcComm
    {
        IrcClient irc;
        public Action<IIrcMessage> ChannelMessageHandlers { get; private set; }
        public Action<IIrcMessage> QueryMessageHandlers { get; private set; }
        public Action<IIrcMessage> TriggerHandlers { get; private set; }
        
        
        public IrcComm(IrcClient ircClient)
        {
            irc = ircClient;
        }
        
        public void AddChannelMessageHandler(Action<IIrcMessage> handler)
        {
            ChannelMessageHandlers += handler;
        }
        public void AddQueryMessageHandler(Action<IIrcMessage> handler)
        {
            QueryMessageHandlers += handler;
        }
        public void AddTriggerHandler(Action<IIrcMessage> handler)
        {
            TriggerHandlers += handler;
        }
        
        
        public void SendMessage(string target, string message, params object[] args)
        {
            SendMessage( target, string.Format(message, args) );
        }
        
        public void SendMessage(string target, string message)
        {
            irc.SendMessage(SendType.Message, target, message);
        }
        
        
        public void DoAction(string target, string action, params object[] args)
        {
            DoAction( target, string.Format(action, args) );
        }
        
        public void DoAction(string target, string action)
        {
            irc.SendMessage(SendType.Action, target, action);
        }
        
        
        public void SendNotice(string target, string message, params object[] args)
        {
            SendNotice( target, string.Format(message, args) );
        }
        
        public void SendNotice(string target, string message)
        {
            irc.SendMessage(SendType.Notice, target, message);
        }
        
        
        public string[] GetChannels()
        {
            return irc.GetChannels();
        }
        public bool IsMe(string nick)
        {
            return irc.IsMe(nick);
        }
    }
}