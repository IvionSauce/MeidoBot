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
    public readonly DateTime SentDateUtc;

    public TimeSpan ElapsedTime
    {
        get
        {
            return DateTime.UtcNow - SentDateUtc;
        }
    }


    public TellEntry(string from, string message)
    {
        From = from;
        Message = message;
        SentDateUtc = DateTime.UtcNow;
    }
}