using System;
using System.Collections.ObjectModel;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    class IrcMsg : IQueryMsg, IChannelMsg, IQueryAction, IChannelAction
    {
        public IIrcComm Irc { get; private set; }
        // Parallel to the IrcMessageData properties.
        public string Message { get; private set; }
        [Obsolete("Use MessageParts instead")]
        public string[] MessageArray
        {
            get
            {
                var array = new string[MessageParts.Count];
                MessageParts.CopyTo(array, 0);
                return array;
            }
        }
        public ReadOnlyCollection<string> MessageParts { get; private set; }
        public string Channel { get; private set; }
        public string Nick { get; private set; }
        public string Ident { get; private set; }
        public string Host { get; private set; }

        // My own additions.
        public string Trigger { get; private set; }
        public string ReturnTo { get; private set; }

        readonly ReceiveType type;


        // Cloning constructor.
        public IrcMsg(IrcMsg msg)
        {
            Irc = msg.Irc;
            type = msg.type;

            Message = msg.Message;
            MessageParts = msg.MessageParts;

            Channel = msg.Channel;
            Nick = msg.Nick;
            Ident = msg.Ident;
            Host = msg.Host;

            Trigger = msg.Trigger;
            ReturnTo = msg.ReturnTo;
        }

        public IrcMsg(IrcComm irc, IrcEventArgs msg, string triggerPrefix)
        {
            Irc = irc;
            type = msg.Data.Type;

            Channel = msg.Data.Channel;
            Nick = msg.Data.Nick;
            Ident = msg.Data.Ident;
            Host = msg.Data.Host;

            ReturnTo = Channel ?? Nick;

            if (msg is ActionEventArgs e)
            {
                Message = e.ActionMessage;
                DoParts(Message.Split(' '));
                // Don't parse trigger if it's an action message.
            }
            else
            {
                Message = msg.Data.Message;
                DoParts(msg.Data.MessageArray);
                Trigger = ParseTrigger(triggerPrefix);
            }
        }

        void DoParts(string[] backingArray)
        {
            MessageParts = new ReadOnlyCollection<string>(backingArray);
        }

        // Returns trigger without the prefix.
        // Will be null if message didn't start with a prefix.
        // Will also be null if prefix occurs at least twice, to escape trigger calling.
        // Will be empty if the prefix was called without a subsequent trigger.
        // In case of a query message it will contain the first word, even if it didn't start with the prefix.
        string ParseTrigger(string prefix)
        {
            if (Message.StartsWith(prefix, StringComparison.Ordinal))
            {
                string trigger = MessageParts[0].Substring(prefix.Length);
                // If trigger starts with prefix suppose it wasn't the intent to call a trigger.
                if (trigger.StartsWith(prefix, StringComparison.Ordinal))
                    return null;
                else
                    return trigger;
            }

            if (type == ReceiveType.QueryMessage)
                return MessageParts[0];
            else
                return null;
        }


        public void SendNotice(string message, params object[] args)
        {
            SendNotice( string.Format(message, args) );
        }

        public void SendNotice(string message)
        {
            Irc.SendNotice(Nick, message);
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
                Irc.SendMessage(Channel, string.Concat(Nick, ": ", message));
                return;
                case ReceiveType.QueryMessage:
                case ReceiveType.QueryAction:
                Irc.SendMessage(Nick, message);
                return;
                default:
                throw new InvalidOperationException("Unexpected ReceiveType.");
            }
        }
    }
}