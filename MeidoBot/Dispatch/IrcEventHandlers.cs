using System;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class IrcEventHandlers
    {
        readonly Dictionary<Type, List<IIrcHandler>> typedHandlers;


        public IrcEventHandlers()
        {
            typedHandlers = new Dictionary<Type, List<IIrcHandler>>();
        }


        public void Add(IIrcHandler handler)
        {
            var handlerList = Get(handler.IrcEventType);
            handlerList.Add(handler);
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