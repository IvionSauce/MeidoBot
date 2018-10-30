using System;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class IrcEventHandlers
    {
        static readonly HashSet<Type> allowedTypes;
        readonly Dictionary<Type, List<IIrcHandler>> typedHandlers;


        static IrcEventHandlers()
        {
            allowedTypes = new HashSet<Type> {
                typeof(IIrcMsg),
                typeof(IChannelMsg),
                typeof(IQueryMsg),
                typeof(IChannelAction),
                typeof(IQueryAction),
                typeof(ITriggerMsg)
            };
            allowedTypes.TrimExcess();
        }

        public IrcEventHandlers()
        {
            typedHandlers = new Dictionary<Type, List<IIrcHandler>>();
        }


        public bool AddHandler(IIrcHandler handler)
        {
            if (allowedTypes.Contains(handler.IrcEventType))
            {
                var handlerList = Get(handler.IrcEventType);
                handlerList.Add(handler);
                return true;
            }

            return false;
        }

        List<IIrcHandler> Get(Type type)
        {
            List<IIrcHandler> handlers;
            if (!typedHandlers.TryGetValue(type, out handlers))
            {
                handlers = new List<IIrcHandler>(1);
                typedHandlers[type] = handlers;
            }

            return handlers;
        }


        public IEnumerable<IIrcHandler> Handlers<T>()
        {
            return HandlersFor(typeof(T));
        }

        public IEnumerable<IIrcHandler> HandlersFor(Type eventType)
        {
            List<IIrcHandler> handlers;
            if (typedHandlers.TryGetValue(eventType, out handlers))
            {
                return handlers;
            }

            return new IIrcHandler[0];
        }
    }
}