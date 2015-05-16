using System;
using System.Collections.Generic;
using MeidoCommon;

namespace MeidoBot
{
    class MeidoComm : IMeidoComm
    {
        public string ConfDir { get; private set; }
        public string DataDir { get; private set; }

        readonly LogFactory logFac;

        readonly Dictionary<string, Trigger> triggers =
            new Dictionary<string, Trigger>(StringComparer.Ordinal);


        readonly Dictionary<string, UserAuth> userAuths =
            new Dictionary<string, UserAuth>(StringComparer.OrdinalIgnoreCase)
        {
            {"Ivion", new UserAuth(null, 10)},
            {"Harime_Nubi", new UserAuth(null, 1)},
            {"SteelGolem", new UserAuth(null, 1)}
        };


        public MeidoComm(LogFactory factory)
        {
            logFac = factory;

            ConfDir = "conf";
            DataDir = "data";
        }


        public ILog CreateLogger(IMeidoHook plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException("plugin");

            string name;
            switch (plugin.Name)
            {
            case "":
            case null:
                name = "Unknown";
                break;
            case "MEIDO":
                name = "_" + plugin.Name;
                break;
            default:
                name = plugin.Name;
                break;
            }

            return logFac.CreateLogger(name);
        }


        public void RegisterTrigger(string trigger, Action<IIrcMessage> callback)
        {
            RegisterTrigger(trigger, callback, false);
        }

        public void RegisterTrigger(string trigger, Action<IIrcMessage> callback, bool needChannel)
        {
            if (trigger == null)
                throw new ArgumentNullException("trigger");
            else if (callback == null)
                throw new ArgumentNullException("callback");

            triggers[trigger] = new Trigger(callback, needChannel);
        }

        public void FireTrigger(IrcMessage msg)
        {
            Trigger tr;
            if (triggers.TryGetValue(msg.Trigger, out tr))
            {
                if (msg.Channel != null || !tr.NeedsChannel)
                    tr.Call(msg);
            }
        }


        public bool Auth(string nick, string pass)
        {
            UserAuth user;
            if (userAuths.TryGetValue(nick, out user))
            {
                if (user.Password.Equals(pass, StringComparison.Ordinal))
                {
                    user.IsAuthenticated = true;
                    return true;
                }
            }
            return false;
        }
        
        
        public int AuthLevel(string nick)
        {
            UserAuth user;
            if (userAuths.TryGetValue(nick, out user))
            {
                if (user.IsAuthenticated)
                    return user.Level;
            }
            return 0;
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


    internal class UserAuth
    {
        internal string Password { get; private set; }
        internal int Level { get; private set; }
        
        bool _auth;
        DateTimeOffset _setTime;
        static readonly TimeSpan maxTime = TimeSpan.FromMinutes(10);
        object _authLock = new object();
        internal bool IsAuthenticated
        {
            get
            {
                if (Password == string.Empty)
                    return true;

                lock (_authLock)
                {
                    if (DateTimeOffset.Now - _setTime < maxTime)
                        return _auth;
                    else
                        return false;
                }
            }
            set
            {
                lock (_authLock)
                {
                    _auth = value;
                    _setTime = DateTimeOffset.Now;
                }
            }
        }
        
        
        internal UserAuth(string pass, int lvl)
        {
            if (string.IsNullOrEmpty(pass))
                Password = string.Empty;
            else
                Password = pass;
            
            const int maxLvl = 10;
            if (lvl > maxLvl)
                Level = maxLvl;
            else if (lvl < 0)
                Level = 0;
            else
                Level = lvl;
        }
    }
}

