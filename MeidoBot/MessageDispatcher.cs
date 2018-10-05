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
        readonly MeidoComm meido;
        readonly string triggerPrefix;

        // Contains nicks to ignore, whether due to abuse or them being other bots.
        volatile Ignores ignore;

        // Standard off-thread queue, to keep the main thread clear.
        readonly Queue<DispatchPackage> Standard;


        public MessageDispatcher(IrcComm ircComm, MeidoComm meidoComm, string triggerPrefix)
        {
            irc = ircComm;
            meido = meidoComm;
            this.triggerPrefix = triggerPrefix;

            Standard = new Queue<DispatchPackage>();
            var t = new Thread(Consume);
            t.Start();
        }

        public void LoadIgnores(string path)
        {
            ignore = Ignores.FromFile(path, meido.Log);
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
                    Enqueue(msg, meido.FireTrigger);

                if (channelHandler != null && msg.Channel != null)
                    Enqueue(msg, channelHandler);
                
                else if (queryHandler != null)
                    Enqueue(msg, queryHandler);
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