using System;
using System.Collections.Generic;
using System.Linq;


class ReplaceHistory
{
    const int MaxMessages = 96;
    readonly Dictionary< string, Queue<MessageItem> > channelToMessages =
        new Dictionary< string, Queue<MessageItem> >(StringComparer.OrdinalIgnoreCase);


    public void AddMessage(string channel, string nick, string message)
    {
        Queue<MessageItem> messages;
        if (!channelToMessages.TryGetValue(channel, out messages))
        {
            messages = new Queue<MessageItem>(MaxMessages);
            channelToMessages[channel] = messages;
        }

        // Discard oldest message when full.
        else if (messages.Count == MaxMessages)
        {
            messages.Dequeue();
        }

        messages.Enqueue(new MessageItem(nick, message));
    }


    public IEnumerable<MessageItem> GetMessages(string channel)
    {
        Queue<MessageItem> messages;
        if (channelToMessages.TryGetValue(channel, out messages))
        {
            // Reverse because enumerating over a queue returns items from oldest to newest.
            // Not ideal, but we'll see if the performance is acceptable before implementing our own data structure.
            return messages.AsEnumerable().Reverse();
        }

        return Enumerable.Empty<MessageItem>();
    }
}


struct MessageItem
{
    public readonly string Nick;
    public readonly string Message;


    public MessageItem (string nick, string message)
    {
        Nick = nick;
        Message = message;
    }
}