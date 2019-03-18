using System;
using System.Collections.Generic;
using MeidoCommon;


namespace MeidoBot
{
    class IrcEventHandlers
    {
        static readonly HashSet<Type> allowedTypes;
        static readonly IIrcHandler[] EmptyHandlers;
        readonly Dictionary<Type, List<IIrcHandler>> typedHandlers;
        readonly Logger log;


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
            EmptyHandlers = new IIrcHandler[0];
        }

        public IrcEventHandlers(Logger log)
        {
            typedHandlers = new Dictionary<Type, List<IIrcHandler>>();
            this.log = log;
        }


        public bool AddHandler(IIrcHandler handler, MeidoPlugin plugin)
        {
            if (allowedTypes.Contains(handler.IrcEventType))
            {
                log.Verbose("{0}: Adding IrcHandler for type '{1}'",
                            plugin.Name, handler.IrcEventType);

                var handlerList = Get(handler.IrcEventType);
                handlerList.Add(handler);
                return true;
            }

            log.Error("{0}: Declared an IrcHandler with unsupported type '{1}'. " +
                      "The method for this type will never be called.",
                      plugin.Name, handler.IrcEventType);
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

            return EmptyHandlers;
        }
    }
}