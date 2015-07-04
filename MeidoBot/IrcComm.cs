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
        public string Nickname
        {
            get { return irc.Nickname; }
        }


        public Action<IIrcMessage> ChannelMessageHandlers { get; private set; }
        public Action<IIrcMessage> ChannelActionHandlers { get; private set; }

        public Action<IIrcMessage> QueryMessageHandlers { get; private set; }
        public Action<IIrcMessage> QueryActionHandlers { get; private set; }

        public Action<IIrcMessage> TriggerHandlers { get; private set; }

        readonly IrcClient irc;
        
        
        public IrcComm(IrcClient ircClient)
        {
            irc = ircClient;
        }

        
        public void AddChannelMessageHandler(Action<IIrcMessage> handler)
        {
            ChannelMessageHandlers += handler;
        }
        public void AddChannelActionHandler(Action<IIrcMessage> handler)
        {
            ChannelActionHandlers += handler;
        }

        public void AddQueryMessageHandler(Action<IIrcMessage> handler)
        {
            QueryMessageHandlers += handler;
        }
        public void AddQueryActionHandler(Action<IIrcMessage> handler)
        {
            QueryActionHandlers += handler;
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
           /* :$nick!$ident@$host PRIVMSG $target :$message\r\n
            * ^     ^      ^     ^^^^^^^^^       ^^        ^^^^
            * 
            * nick+ident+host+target+message + 16 <= 512 */

            var user = irc.GetIrcUser(irc.Nickname);
            // Count all the non-message characters.
            int count = 16 +
                irc.Nickname.Length +
                user.Ident.Length +
                user.Host.Length +
                target.Length;

            int maxMsgLength = 512 - count;
            var messages = MessageTools.Split(message, maxMsgLength);

            foreach (string msg in messages)
                irc.SendMessage(SendType.Message, target, msg);
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
           /* :$nick!$ident@$host NOTICE $target :$message\r\n
            * ^     ^      ^     ^^^^^^^^       ^^        ^^^^
            * 
            * nick+ident+host+target+message + 15 <= 512 */

            var user = irc.GetIrcUser(irc.Nickname);
            // Count all the non-message characters.
            int count = 15 +
                irc.Nickname.Length +
                user.Ident.Length +
                user.Host.Length +
                target.Length;

            int maxMsgLength = 512 - count;
            var messages = MessageTools.Split(message, maxMsgLength);

            foreach (string msg in messages)
                irc.SendMessage(SendType.Notice, target, msg);
        }
        
        
        public string[] GetChannels()
        {
            return irc.GetChannels();
        }

        public bool IsMe(string nick)
        {
            return irc.IsMe(nick);
        }

        public bool IsJoined(string channel, string nick)
        {
            return irc.IsJoined(channel, nick);
        }
    }
}