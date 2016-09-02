using System;
using System.Runtime.Serialization;


[DataContract]
class TellEntry
{
    [DataMember]
    public readonly string From;
    [DataMember]
    public readonly string Message;
    [DataMember]
    public readonly DateTimeOffset SentDate;

    public TimeSpan ElapsedTime
    {
        get
        {
            return DateTimeOffset.Now - SentDate;
        }
    }


    public TellEntry(string from, string message)
    {
        From = from;
        Message = message;
        SentDate = DateTimeOffset.Now;
    }
}