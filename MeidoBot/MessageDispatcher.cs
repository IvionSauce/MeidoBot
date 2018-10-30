using System;
using System.Threading;
using System.Collections.Generic;
using Meebey.SmartIrc4net;
using MeidoCommon;


namespace MeidoBot
{
    class MessageDispatcher : IDisposable
    {
        // Contains nicks to ignore, whether due to abuse or them being other bots.
        public volatile Ignores Ignore;

        readonly IrcComm irc;
        readonly Triggers triggers;
        readonly string triggerPrefix;

        // Standard off-thread queue, to keep the main thread clear.
        readonly Queue<Action> Standard;

        // Queues, either on plugin level or seperate for each trigger.
        readonly Dictionary<Trigger, Queue<Action>> triggerQueues;


        public MessageDispatcher(IrcComm ircComm, Triggers triggers, string triggerPrefix)
        {
            irc = ircComm;
            this.triggers = triggers;
            this.triggerPrefix = triggerPrefix;

            Standard = new Queue<Action>();
            StartConsumeThread(Standard);

            triggerQueues = new Dictionary<Trigger, Queue<Action>>();
        }


        // --- Event handlers for SmartIrc4Net ---

        public void OnMessage(object sender, IrcEventArgs e)
        {
            DoHandlers(e, irc.ChannelMessageHandlers, irc.QueryMessageHandlers);
        }

        public void OnAction(object sender, ActionEventArgs e)
        {
            DoHandlers(e, irc.ChannelActionHandlers, irc.QueryActionHandlers);
        }


        // --- Dispatching the correct things in the correct way ---

        void DoHandlers(IrcEventArgs e, Action<IIrcMessage> channelHandler, Action<IIrcMessage> queryHandler)
        {
            var msg = new IrcMessage(irc, e.Data, triggerPrefix);

            if (!Ignore.Contains(msg.Nick))
            {
                if (msg.Trigger != null)
                    HandleTrigger(msg);

                if (channelHandler != null && msg.Channel != null)
                    Push(msg, channelHandler, Standard);
                
                else if (queryHandler != null)
                    Push(msg, queryHandler, Standard);
            }
        }

        void HandleTrigger(IrcMessage msg)
        {
            Trigger trigger;
            if (triggers.TryGet(msg.Trigger, out trigger))
            {
                // • Unique queue: queue per trigger
                //    trigger -> Queue [one-to-one]

                // • Shared queue: queue per plugin
                //    triggers -> Queue [many-to-one]

                // • Threadpool: Reentrant chaos
                //    triggers -> Threadpool [many-to-many]

                // • Standard/Default: single shared queue for all
                //    * -> Queue [many-to-one]

                // Only the last 3 are implemented for now.
                switch (trigger.Threading)
                {
                    case TriggerThreading.Queue:
                    Queue<Action> queue;
                    if (triggerQueues.TryGetValue(trigger, out queue))
                    {
                        Push(triggers.Delegate(trigger, msg), queue);
                        break;
                    }
                    goto default;

                    case TriggerThreading.Threadpool:
                    ThreadPool.QueueUserWorkItem( (cb) => triggers.Fire(trigger, msg) );
                    break;

                    default:
                    Push(triggers.Delegate(trigger, msg), Standard);
                    break;
                }
            }
        }


        // --- Enqueing, consuming and threading ---

        static void Push<T>(T ircEvent, Action<T> action, Queue<Action> q)
        {
            Push(() => action(ircEvent), q);
        }

        static void Push(Action action, Queue<Action> q)
        {
            lock (q)
            {
                q.Enqueue(action);
                Monitor.Pulse(q);
            }
        }


        public void ProcessPluginQueues(MeidoPlugin plugin)
        {
            // Shared queue for all triggers declared by the plugin,
            // that is if they have opted for Threading.Queue.
            Queue<Action> queue = null;

            foreach (var tr in plugin.Triggers)
            {
                if (tr.Threading == TriggerThreading.Queue)
                {
                    if (queue == null)
                        queue = new Queue<Action>();

                    triggerQueues[tr] = queue;
                }
                // We don't need to do anything for other types of threading.
            }

            if (queue != null)
                StartConsumeThread(queue);
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
        }
    }
}