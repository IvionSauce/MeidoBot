using System;
using System.Threading;
using System.Collections.Generic;
using Meebey.SmartIrc4net;
using MeidoCommon;


namespace MeidoBot
{
    class Dispatcher : IDisposable
    {
        // Contains nicks to ignore, whether due to abuse or them being other bots.
        public volatile Ignores Ignore;

        readonly IrcComm irc;
        readonly Triggers triggers;
        readonly IrcEventHandlers ircEvents;
        readonly string triggerPrefix;

        // Standard off-thread queue, to keep the main thread clear.
        readonly Queue<Action> Standard;

        // Queues, either on plugin level or seperate for each trigger or IRC event handler.
        readonly Dictionary<Trigger, Queue<Action>> triggerQueues;
        readonly Dictionary<IIrcHandler, Queue<Action>> eventQueues;


        public Dispatcher(IrcComm ircComm, Triggers triggers, string triggerPrefix)
        {
            irc = ircComm;
            this.triggers = triggers;
            ircEvents = new IrcEventHandlers();
            this.triggerPrefix = triggerPrefix;

            Standard = new Queue<Action>();
            StartConsumeThread(Standard);

            triggerQueues = new Dictionary<Trigger, Queue<Action>>();
            eventQueues = new Dictionary<IIrcHandler, Queue<Action>>();
        }


        // --- Event handlers for SmartIrc4Net ---

        public void ChannelMessage(object sender, IrcEventArgs e)
        {
            var msg = new IrcMsg(irc, e.Data, triggerPrefix);
            if (!Ignore.Contains(msg.Nick))
            {
                DoTrigger(msg);
                DoHandlers<IChannelMsg>(msg);
                DoHandlers<IIrcMsg>(msg);
            }
        }

        public void QueryMessage(object sender, IrcEventArgs e)
        {
            var msg = new IrcMsg(irc, e.Data, triggerPrefix);
            if (!Ignore.Contains(msg.Nick))
            {
                DoTrigger(msg);
                DoHandlers<IQueryMsg>(msg);
                DoHandlers<IIrcMsg>(msg);
            }
        }

        public void ChannelAction(object sender, ActionEventArgs e)
        {
            var msg = new IrcMsg(irc, e.Data, triggerPrefix);
            if (!Ignore.Contains(msg.Nick))
            {
                DoHandlers<IChannelAction>(msg);
                DoHandlers<IIrcMsg>(msg);
            }
        }

        public void QueryAction(object sender, ActionEventArgs e)
        {
            var msg = new IrcMsg(irc, e.Data, triggerPrefix);
            if (!Ignore.Contains(msg.Nick))
            {
                DoHandlers<IQueryAction>(msg);
                DoHandlers<IIrcMsg>(msg);
            }
        }

        // --- Dispatching the correct things in the correct way ---

        // • Unique queue: queue per trigger/handler
        //   [one-to-one]

        // • Shared queue: queue per plugin
        //   [many-to-one]

        // • Threadpool: Reentrant chaos
        //   [many-to-many]

        // • Standard/Default: single shared queue for all
        //   [many-to-one]

        // Only the last 3 are implemented for now.

        void DoTrigger(IrcMsg msg)
        {
            // Return early if there's nothing to do.
            if (string.IsNullOrEmpty(msg.Trigger))
                return;
            
            Trigger trigger;
            // Enqueue specific trigger call.
            if (triggers.TryGet(msg.Trigger, out trigger))
            {
                ThreadingDispatch(
                    trigger.Threading,
                    triggers.Delegate(trigger, msg),
                    (cb) => triggers.Fire(trigger, msg),
                    trigger, triggerQueues
                );
            }
            // Enqueue general trigger event.
            DoHandlers<ITriggerMsg>(msg);
        }

        void DoHandlers<T>(T ircEvent)
        {
            // Enqueue IRC handlers for raised event type.
            foreach (var handler in ircEvents.Handlers<T>())
            {
                ThreadingDispatch(
                    handler.Threading,
                    () => handler.Invoke(ircEvent),
                    (cb) => handler.Invoke(ircEvent),
                    handler, eventQueues
                );
            }
        }


        void ThreadingDispatch<T>(
            TriggerThreading threading,
            Action queueDelegate,
            WaitCallback threadpoolDelegate,
            T id,
            Dictionary<T, Queue<Action>> map)
        {
            switch (threading)
            {
                case TriggerThreading.Queue:
                Queue<Action> queue;
                if (map.TryGetValue(id, out queue))
                {
                    Push(queueDelegate, queue);
                    break;
                }
                goto default;

                case TriggerThreading.Threadpool:
                ThreadPool.QueueUserWorkItem(threadpoolDelegate);
                break;

                default:
                Push(queueDelegate, Standard);
                break;
            }
        }


        // --- Enqueing, consuming and threading ---

        static void Push(Action action, Queue<Action> q)
        {
            lock (q)
            {
                q.Enqueue(action);
                Monitor.Pulse(q);
            }
        }


        public void ProcessPluginDeclares(MeidoPlugin plugin)
        {
            // Shared queue for all triggers or IRC even handlers declared by the plugin,
            // that is if they have opted for ThreadingModel.Queue.
            Queue<Action> queue = null;

            foreach (var tr in plugin.Triggers)
            {
                if (triggers.AddTrigger(tr, plugin))
                    RegisterThreading(tr.Threading, tr, triggerQueues, ref queue);
            }
            foreach (var handler in plugin.Handlers)
            {
                if (ircEvents.AddHandler(handler))
                    RegisterThreading(handler.Threading, handler, eventQueues, ref queue);
            }

            if (queue != null)
                StartConsumeThread(queue);
        }

        // Map `id` to `queue` via a Dictionary in a way determined by ThreadingModel.
        static void RegisterThreading<T>(
            TriggerThreading threading,
            T id,
            Dictionary<T, Queue<Action>> map,
            ref Queue<Action> queue)
        {
            if (threading == TriggerThreading.Queue)
            {
                if (queue == null)
                    queue = new Queue<Action>();

                map[id] = queue;
            }
            // We don't need to do anything for other types of threading.
        }


        static void StartConsumeThread(Queue<Action> queue)
        {
            var t = new Thread(Consume);
            t.Start(queue);
        }

        static void Consume(object queueObj)
        {
            var q = (Queue<Action>)queueObj;
            Action action;

            while (true)
            {
                lock (q)
                {
                    while (q.Count == 0)
                        Monitor.Wait(q);

                    action = q.Dequeue();
                }

                if (action != null)
                    action();
                else
                    return;
            }
        }


        public void Dispose()
        {
            Push(null, Standard);
            foreach (var queue in triggerQueues.Values)
            {
                Push(null, queue);
            }
            foreach (var queue in eventQueues.Values)
            {
                Push(null, queue);
            }
        }
    }
}