using System;
using System.Collections.Generic;
using System.Runtime.Serialization;


[DataContract]
class TellsInbox
{
    [DataMember]
    public readonly string Username;

    [DataMember]
    public int MessagesCount { get; private set; }
    public int Capacity
    {
        get { return entries.Length; }
    }

    [DataMember]
    public bool NewMessages { get; set; }

    const int StdMaxMessages = 30;
    [DataMember]
    readonly TellEntry[] entries;
    bool isSorted;


    static public TellsInbox Empty = new TellsInbox(string.Empty, 0);


    public TellsInbox(string username) : this(username, StdMaxMessages) {}

    public TellsInbox(string username, int maxMessages)
    {
        Username = username;
        MessagesCount = 0;
        NewMessages = false;
        entries = new TellEntry[maxMessages];
        isSorted = false;
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
                isSorted = false;
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

        if (!isSorted)
            Sort(entries);

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
        isSorted = true;

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
            if (entries[i] != null && predicate(entries[i]))
            {
                entries[i] = null;
                deleted++;
            }
        }

        MessagesCount = MessagesCount - deleted;
        isSorted = false;
        return deleted;
    }


    static void Sort(TellEntry[] arr)
    {
        var compare = Comparer<TellEntry>.Create(
            (a, b) => {
                // Correctly handly if either, or both, are null.
                if (a == null)
                {
                    if (b == null)
                        return 0;
                    
                    return 1;
                }
                if (b == null)
                    return -1;

                // Otherwise just sort by SentDate.
                return a.SentDateUtc.CompareTo(b.SentDateUtc);
            }
        );

        Array.Sort(arr, compare);
    }
}