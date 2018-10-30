using System;


namespace MeidoCommon
{
    public interface IIrcHandler
    {
        Type IrcEventType { get; }
        TriggerThreading Threading { get; }

        void Invoke(object ircEvent);
    }


    public class IrcHandler<T> : IIrcHandler
        where T : class
    {
        public Type IrcEventType { get; private set; }
        public TriggerThreading Threading { get; private set; }

        readonly Action<T> handler;


        public IrcHandler(Action<T> handler) : this(handler, TriggerThreading.Default) {}

        public IrcHandler(Action<T> handler, TriggerThreading threading)
        {
            IrcEventType = typeof(T);
            this.handler = handler;
            Threading = threading;
        }


        public void Invoke(T ircEvent)
        {
            if (ircEvent == null)
                throw new ArgumentNullException(nameof(ircEvent));
            
            handler(ircEvent);
        }

        public void Invoke(object ircEvent)
        {
            var TEvent = ircEvent as T;
            if (TEvent != null)
            {
                handler(TEvent);
            }
        }
    }


    public static class IrcHandler
    {
        public static IrcHandler<T> New<T>(Action<T> handler)
            where T : class
        {
            return new IrcHandler<T>(handler);
        }

        public static IrcHandler<T> New<T>(Action<T> handler, TriggerThreading threading)
            where T : class
        {
            return new IrcHandler<T>(handler, threading);
        }
    }
}