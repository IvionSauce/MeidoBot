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
        readonly UserAuthManager userAuths;

        readonly Logger log;
        readonly Dictionary<string, Trigger> triggers =
            new Dictionary<string, Trigger>(StringComparer.Ordinal);


        public MeidoComm(LogFactory factory)
        {
            logFac = factory;
            log = logFac.CreateLogger("MEIDO");

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

            log.Verbose("Registering trigger '{0}'.", trigger);
            triggers[trigger] = new Trigger(callback, needChannel);
        }

        public void FireTrigger(IrcMessage msg)
        {
            Trigger tr;
            if (triggers.TryGetValue(msg.Trigger, out tr))
            {
                if (msg.Channel != null || !tr.NeedsChannel)
                {
                    string source = msg.Channel ?? "PM";
                    log.Message("{0}/{1} {2}", source, msg.Nick, msg.Message);

                    tr.Call(msg);
                }
            }
        }

        public void ClearTriggers()
        {
            triggers.Clear();
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


    class Trigger
    {
        public readonly Action<IIrcMessage> Call;
        public readonly bool NeedsChannel;

        public Trigger(Action<IIrcMessage> call, bool needChannel)
        {
            Call = call;
            NeedsChannel = needChannel;
        }
    }
}

