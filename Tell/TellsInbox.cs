﻿using System.Runtime.Serialization;


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


    public bool TryReadNext(out TellEntry entry)
    {
        entry = NextEntry();
        if (entry != null)
        {
            NewMessages = false;
            return true;
        }

        return false;
    }

    TellEntry NextEntry()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null)
            {
                var entry = entries[i];

                entries[i] = null;
                MessagesCount--;

                return entry;
            }
        }

        return null;
    }


    public bool TryRead(int msgNo, out TellEntry entry)
    {
        if (msgNo >= 0 && msgNo < entries.Length)
        {
            entry = entries[msgNo];
            if (entry != null)
            {
                entries[msgNo] = null;
                MessagesCount--;
                NewMessages = false;
                return true;
            }
        }

        entry = null;
        return false;
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

}