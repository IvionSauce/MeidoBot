using System;
using System.Collections.ObjectModel;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    class IrcMsg : IQueryMsg, IChannelMsg, ITriggerMsg, IQueryAction, IChannelAction
    {
        public IIrcComm Irc { get; private set; }
        // Parallel to the IrcMessageData properties.
        public string Message { get; private set; }
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


        public IrcMsg(IrcComm irc, ActionEventArgs e, string prefix) : this(irc, e.Data, prefix)
        {
            // SmartIrc4Net already chops off the control character for us,
            // so use that.
            Message = e.ActionMessage;
            DoParts(Message.Split(' '));
        }

        public IrcMsg(IrcComm irc, IrcMessageData messageData, string prefix)
        {
            Irc = irc;
            type = messageData.Type;

            Message = messageData.Message;
            DoParts(messageData.MessageArray);

            Channel = messageData.Channel;
            Nick = messageData.Nick;
            Ident = messageData.Ident;
            Host = messageData.Host;

            Trigger = ParseTrigger(prefix);
            ReturnTo = Channel ?? Nick;
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
            // Don't parse trigger if it's an action message.
            if (type == ReceiveType.ChannelAction || type == ReceiveType.QueryAction)
                return null;

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