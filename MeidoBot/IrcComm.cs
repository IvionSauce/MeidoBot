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
        
        
        public void SendMessage(string target, string message, params object[] args)
        {
            SendMessage( target, string.Format(message, args) );
        }
        
        public void SendMessage(string target, string message)
        {
            const string command = "PRIVMSG";

            int maxMsgLength = 512 - CountNonMessageCharacters(command.Length, target.Length);
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
            const string command = "NOTICE";

            int maxMsgLength = 512 - CountNonMessageCharacters(command.Length, target.Length);
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


        int CountNonMessageCharacters(int commandLength, int targetLength)
        {
           /* :$nick!$ident@$host $command $target :$message\r\n
            * ^     ^      ^     ^        ^       ^^        ^ ^
            * 
            * nick+ident+host+command+target+message + 9 <= 512 */

            int count = 9 +
                irc.Nickname.Length +
                commandLength +
                targetLength;

            // Will return null if it can't find the specified nick. Since we're requesting our own nick it would lead
            // us to believe that if we get null back there's something wrong. Most likely there's a connection error.
            var user = irc.GetIrcUser(irc.Nickname);
            if (user != null)
            {
                // If we can, allow the server to inform us about our Ident and Hostname. The former is the Username
                // which may or may not be prefixed. The latter might differ because our Hostname might be too long or
                // our Hostname is a Vhost of some kind.
                // Because of these variants it is preferable to ask the server rather than work it out ourselves.
                count += user.Ident.Length + user.Host.Length;
            }
            // Nevertheless fall back to sane defaults, even though we're most likely no longer connected.
            else
            {
                const int maxHostnameLength = 63;

                // `Username+1` because the Ident is often the Username prefixed with a certain symbol (example: ~).
                // Assume max length for Hostname to be on the safe side.
                count += 1 + irc.Username.Length + maxHostnameLength;
            }

            return count;
        }
    }
}