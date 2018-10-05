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

        readonly Dictionary<string, Queue<DispatchPackage>> triggerQueues;


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
            var t = new Thread(Consume);
            t.Start();

            triggerQueues = new Dictionary<string, Queue<DispatchPackage>>();
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
                    HandleTrigger(msg, TriggerThreading.Queue);

                if (channelHandler != null && msg.Channel != null)
                    Enqueue(msg, channelHandler);
                
                else if (queryHandler != null)
                    Enqueue(msg, queryHandler);
            }
        }

        void HandleTrigger(IrcMessage msg, TriggerThreading threading)
        {
            // Unique Queue: trigger -> Queue [one-to-one]
            // Shared Queue: triggers -> Queue [many-to-one]
            // Threadpool: Reentrant chaos
            switch (threading)
            {
                case TriggerThreading.Queue:
                Queue<DispatchPackage> queue;
                if (triggerQueues.TryGetValue(msg.Trigger, out queue))
                {
                    Push(queue, msg, triggers.FireTrigger);
                    break;
                }
                goto default;

                case TriggerThreading.Threadpool:
                ThreadPool.QueueUserWorkItem( (cb) => triggers.FireTrigger(msg) );
                break;

                default:
                Push(Standard, msg, triggers.FireTrigger);
                break;
            }
        }


        // --- Enqueing and threading ---

        void Enqueue(IrcMessage msg, Action<IIrcMessage> action)
        {
            var disPackage = new DispatchPackage(msg, action);
            lock (Standard)
            {
                Standard.Enqueue(disPackage);
                Monitor.Pulse(Standard);
            }
        }


        void Consume()
        {
            DispatchPackage pack;
            while (true)
            {
                lock (Standard)
                {
                    while (Standard.Count == 0)
                        Monitor.Wait(Standard);

                    pack = Standard.Dequeue();
                }

                if (pack != null)
                    pack.Apply();
                else
                    return;
            }
        }


        static void Push(Queue<DispatchPackage> q, IrcMessage msg, Action<IIrcMessage> action)
        {
            var package = new DispatchPackage(msg, action);
            lock (q)
            {
                q.Enqueue(package);
                Monitor.Pulse(q);
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


        public void Apply()
        {
            action(message);
        }
    }
}