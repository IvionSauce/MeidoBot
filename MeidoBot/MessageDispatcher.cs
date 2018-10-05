﻿using System;
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
            StartConsumeThread(Standard);

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
                    HandleTrigger(msg);

                if (channelHandler != null && msg.Channel != null)
                    Push(msg, channelHandler, Standard);
                
                else if (queryHandler != null)
                    Push(msg, queryHandler, Standard);
            }
        }

        void HandleTrigger(IrcMessage msg)
        {
            // Unique Queue: trigger -> Queue [one-to-one]
            // Shared Queue: triggers -> Queue [many-to-one]
            // Threadpool: Reentrant chaos
            switch ( triggers.GetThreading(msg.Trigger) )
            {
                case TriggerThreading.Queue:
                Queue<DispatchPackage> queue;
                if (triggerQueues.TryGetValue(msg.Trigger, out queue))
                {
                    Push(msg, triggers.FireTrigger, queue);
                }
                goto default;

                case TriggerThreading.Threadpool:
                ThreadPool.QueueUserWorkItem( (cb) => triggers.FireTrigger(msg) );
                break;

                default:
                Push(msg, triggers.FireTrigger, Standard);
                break;
            }
        }


        // --- Enqueing and threading ---

        static void Push(IrcMessage msg, Action<IIrcMessage> action, Queue<DispatchPackage> q)
        {
            var package = new DispatchPackage(msg, action);
            lock (q)
            {
                q.Enqueue(package);
                Monitor.Pulse(q);
            }
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
                    pack.Apply();
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


        public void Apply()
        {
            action(message);
        }
    }
}