using System;


namespace MeidoCommon
{
    public enum TriggerThreading
    {
        Default,
        Queue,
        Threadpool
    }

    public enum TriggerOptions
    {
        None,
        ChannelOnly,
        QueryOnly
    }


    public class Trigger
    {
        public readonly string Identifier;
        public readonly Action<IIrcMessage> Call;
        public readonly TriggerOptions Options;
        public readonly TriggerThreading Threading;


        public Trigger(string identifier, Action<IIrcMessage> call) :
        this(identifier, call, TriggerOptions.None, TriggerThreading.Default) {}

        public Trigger(string identifier, Action<IIrcMessage> call, TriggerOptions opts) :
        this(identifier, call, opts, TriggerThreading.Default) {}

        public Trigger(string identifier, Action<IIrcMessage> call, TriggerThreading threading) :
        this(identifier, call, TriggerOptions.None, threading) {}

        public Trigger(
            string identifier,
            Action<IIrcMessage> call,
            TriggerOptions opts,
            TriggerThreading threading)
        {
            if (identifier == null)
                throw new ArgumentNullException(nameof(identifier));
            if (call == null)
                throw new ArgumentNullException(nameof(call));
            if (identifier == string.Empty)
                throw new ArgumentException("Cannot be empty.", nameof(identifier));
            if (ContainsWhitespace(identifier))
                throw new ArgumentException("Cannot contain whitespace.", nameof(identifier));

            Identifier = identifier;
            Call = call;
            Options = opts;
            Threading = threading;
        }


        static bool ContainsWhitespace(string s)
        {
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c))
                    return true;
            }

            return false;
        }
    }
}