using System;
using System.Threading;
using System.Collections.Generic;
using Meebey.SmartIrc4net;
using MeidoCommon;


namespace MeidoBot
{
    class MessageDispatcher
    {
        readonly IrcComm irc;
        readonly Triggers triggers;
        readonly string triggerPrefix;
        readonly Logger log;

        // Contains nicks to ignore, whether due to abuse or them being other bots.
        volatile Ignores ignore;

        // Standard off-thread queue, to keep the main thread clear.
        readonly Queue<DispatchPackage> Standard;

        // Queues, either on plugin level or seperate for each trigger.
        readonly Dictionary<Trigger, Queue<DispatchPackage>> triggerQueues;


        public MessageDispatcher(
            IrcComm ircComm,
            Triggers triggers,
            string triggerPrefix,
            Logger log)
        {
            irc = ircComm;
            this.triggers = triggers;
            this.triggerPrefix = triggerPrefix;
            this.log = log;

            Standard = new Queue<DispatchPackage>();
            StartConsumeThread(Standard);

            triggerQueues = new Dictionary<Trigger, Queue<DispatchPackage>>();
            triggers.PluginTriggersRegister += ProcessPluginTriggers;
        }

        public void LoadIgnores(string path)
        {
            ignore = Ignores.FromFile(path, log);
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

            if (!ignore.Contains(msg.Nick))
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
                    Queue<DispatchPackage> queue;
                    if (triggerQueues.TryGetValue(trigger, out queue))
                    {
                        Push(msg, triggers.Delegate(trigger), queue);
                        break;
                    }
                    goto default;

                    case TriggerThreading.Threadpool:
                    ThreadPool.QueueUserWorkItem( (cb) => triggers.Fire(trigger, msg) );
                    break;

                    default:
                    Push(msg, triggers.Delegate(trigger), Standard);
                    break;
                }
            }
        }


        // --- Enqueing, consuming and threading ---

        static void Push(IrcMessage msg, Action<IIrcMessage> action, Queue<DispatchPackage> q)
        {
            var package = new DispatchPackage(msg, action);
            lock (q)
            {
                q.Enqueue(package);
                Monitor.Pulse(q);
            }
        }


        void ProcessPluginTriggers(IEnumerable<Trigger> pTriggers)
        {
            // Shared queue for all triggers declared by the plugin,
            // that is if they have opted for Threading.Queue.
            Queue<DispatchPackage> queue = null;

            foreach (var tr in pTriggers)
            {
                if (tr.Threading == TriggerThreading.Queue)
                {
                    if (queue == null)
                        queue = new Queue<DispatchPackage>();

                    triggerQueues[tr] = queue;
                }
                // We don't need to do anything for other types of threading.
            }

            if (queue != null)
                StartConsumeThread(queue);
        }


        static void StartConsumeThread(Queue<DispatchPackage> queue)
        {
            var t = new Thread(Consume);
            t.Start(queue);
        }

        static void Consume(object queueObj)
        {
            var q = (Queue<DispatchPackage>)queueObj;
            DispatchPackage pack;

            while (true)
            {
                lock (q)
                {
                    while (q.Count == 0)
                        Monitor.Wait(q);

                    pack = q.Dequeue();
                }

                if (pack != null)
                    pack.Invoke();
                else
                    return;
            }
        }
    }


    class DispatchPackage
    {
        readonly IrcMessage message;
        readonly Action<IIrcMessage> action;

        public DispatchPackage(IrcMessage msg, Action<IIrcMessage> action)
        {
            message = msg;
            this.action = action;
        }


        public void Invoke()
        {
            action(message);
        }
    }
}