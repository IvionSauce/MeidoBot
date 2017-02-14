using System;
using System.Runtime.Serialization;


[DataContract]
class TellsInbox
{
    [DataMember]
    public readonly string Username;
    [DataMember]
    public int MessagesCount { get; private set; }
    [DataMember]
    public bool NewMessages { get; set; }

    const int StdMaxMessages = 30;
    [DataMember]
    readonly TellEntry[] entries;


    static public TellsInbox Empty = new TellsInbox(string.Empty, 0);


    public TellsInbox(string username) : this(username, StdMaxMessages) {}

    public TellsInbox(string username, int maxMessages)
    {
        Username = username;
        MessagesCount = 0;
        NewMessages = false;
        entries = new TellEntry[maxMessages];
    }


    public bool Add(string from, string tellMsg)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null)
            {
                entries[i] = new TellEntry(from, tellMsg);
                MessagesCount++;
                NewMessages = true;
                return true;
            }
        }

        return false;
    }


    public TellEntry[] Read(int amount)
    {
        TellEntry[] messages;
        if (amount > MessagesCount)
            messages = new TellEntry[MessagesCount];
        else
            messages = new TellEntry[amount];

        // Index over internal tell entries.
        int inboxIdx = 0;
        // Index over outgoing 'read' messages.
        int messagesIdx = 0;

        while (inboxIdx < entries.Length && messagesIdx < messages.Length)
        {
            if (entries[inboxIdx] != null)
            {
                messages[messagesIdx] = entries[inboxIdx];
                entries[inboxIdx] = null;
                messagesIdx++;
            }

            inboxIdx++;
        }

        MessagesCount = MessagesCount - messages.Length;
        NewMessages = false;

        return messages;
    }


    public void ClearMessages()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = null;
        }
        MessagesCount = 0;
        NewMessages = false;
    }


    public int DeleteMessages(Func<TellEntry, bool> predicate)
    {
        int deleted = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (predicate(entries[i]))
            {
                entries[i] = null;
                deleted++;
            }
        }

        MessagesCount = MessagesCount - deleted;
        return deleted;
    }

}