using System;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    // Implement IIrcMessage and supply a constructor that fills in the fields, once again allowing the subset of
    // a SmartIrc4Net class to trickle down to the plugins.
    class IrcMessage : IIrcMessage
    {
        // Identical to the IrcMessageData properties.
        public string Message { get; private set; }
        public string[] MessageArray { get; private set; }
        public string Channel { get; private set; }
        public string Nick { get; private set; }
        public string Ident { get; private set; }
        public string Host { get; private set; }

        // My own additions.
        public string Trigger { get; private set; }
        public string ReturnTo { get; private set; }
        
        readonly IrcClient irc;
        readonly ReceiveType type;
        
        
        public IrcMessage(Meebey.SmartIrc4net.IrcMessageData messageData, string prefix)
        {
            irc = messageData.Irc;
            type = messageData.Type;
            
            Message = messageData.Message;
            MessageArray = messageData.MessageArray;
            Channel = messageData.Channel;
            Nick = messageData.Nick;
            Ident = messageData.Ident;
            Host = messageData.Host;
            
            Trigger = ParseTrigger(prefix);
            ReturnTo = Channel ?? Nick;
        }
        
        
        // Returns trigger without the prefix.
        // Will be null if message didn't start with a prefix.
        // Will also be null if prefix occurs at least twice, to escape trigger calling.
        // Will be empty if the prefix was called without a subsequent trigger.
        // In case of a query message it will contain the first word, even if it didn't start with the prefix.
        string ParseTrigger(string prefix)
        {
            // Don't parse trigger if it's an action message.
            if (type == ReceiveType.ChannelAction || type == ReceiveType.QueryAction)
                return null;

            if (Message.StartsWith(prefix, StringComparison.Ordinal))
            {
                string trigger = MessageArray[0].Substring(prefix.Length);
                // If trigger starts with prefix suppose it wasn't the intent to call a trigger.
                if (trigger.StartsWith(prefix, StringComparison.Ordinal))
                    return null;
                else
                    return trigger;
            }

            if (type == ReceiveType.QueryMessage)
                return MessageArray[0];
            else
                return null;
        }


        public void SendNotice(string message, params object[] args)
        {
            SendNotice( string.Format(message, args) );
        }

        public void SendNotice(string message)
        {
            irc.SendMessage(SendType.Notice, Nick, message);
        }
        
        
        public void Reply(string message, params object[] args)
        {
            Reply( string.Format(message, args) );
        }
        
        public void Reply(string message)
        {
            switch(type)
            {
            case ReceiveType.ChannelMessage:
            case ReceiveType.ChannelAction:
                irc.SendMessage(SendType.Message, Channel, string.Concat(Nick, ": ", message));
                return;
            case ReceiveType.QueryMessage:
            case ReceiveType.QueryAction:
                irc.SendMessage(SendType.Message, Nick, message);
                return;
            default:
                throw new InvalidOperationException("Unexpected ReceiveType.");
            }
        }
    }
}