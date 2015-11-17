using System;
using System.Collections.Generic;
using MeidoCommon;
using Meebey.SmartIrc4net;


namespace MeidoBot
{
    class MeidoComm : IMeidoComm
    {
        public string ConfDir { get; private set; }
        public string DataDir { get; private set; }

        readonly LogFactory logFac;
        readonly Triggers triggers;
        readonly UserAuthManager userAuths;


        public MeidoComm(LogFactory factory)
        {
            logFac = factory;
            triggers = new Triggers(factory.CreateLogger("MEIDO"));

            ConfDir = "conf";
            DataDir = "data";

            string authPath = System.IO.Path.Combine(ConfDir, "Auth.xml");
            userAuths = new UserAuthManager(authPath, logFac.CreateLogger("AUTH"));
        }


        public ILog CreateLogger(IMeidoHook plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException("plugin");

            return logFac.CreateLogger(plugin);
        }


        public void RegisterTrigger(string trigger, Action<IIrcMessage> callback)
        {
            RegisterTrigger(trigger, callback, false);
        }

        public void RegisterTrigger(string trigger, Action<IIrcMessage> callback, bool needChannel)
        {
            if (trigger == null)
                throw new ArgumentNullException("trigger");
            else if (trigger == string.Empty)
                throw new ArgumentException("Cannot be an empty string.", "trigger");
            else if (callback == null)
                throw new ArgumentNullException("callback");

            triggers.RegisterTrigger(trigger, new Trigger(callback, needChannel));
        }

        public void FireTrigger(IrcMessage msg)
        {
            triggers.FireTrigger(msg);
        }


        public bool Auth(string nick, string pass)
        {
            if (nick == null)
                throw new ArgumentNullException("nick");

            return userAuths.Authenticate(nick, pass);
        }

        public int AuthLevel(string nick)
        {
            if (nick == null)
                throw new ArgumentNullException("nick");

            return userAuths.AuthLevel(nick);
        }
    }
}

