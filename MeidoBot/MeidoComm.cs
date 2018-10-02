using System;
using System.IO;
using MeidoCommon;


namespace MeidoBot
{
    class MeidoComm : IMeidoComm
    {
        public string ConfDir { get; private set; }
        public string DataDir { get; private set; }

        readonly LogFactory logFac;
        readonly Triggers triggers;
        readonly WatchConfig watcher;
        readonly UserAuthManager userAuths;


        public MeidoComm(MeidoConfig conf, ThrottleManager tManager, LogFactory factory)
        {
            ConfDir = conf.ConfigurationDirectory;
            DataDir = conf.DataDirectory;

            logFac = factory;
            triggers = new Triggers(tManager, logFac.CreateLogger("Meido"));
            watcher = new WatchConfig(ConfDir, logFac.CreateLogger("Meido"));

            string authPath = ConfPathTo("Auth.xml");
            userAuths = new UserAuthManager(authPath, logFac.CreateLogger("Auth"));
        }


        public ILog CreateLogger(IMeidoHook plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            return logFac.CreateLogger(plugin);
        }


        public void RegisterTrigger(string trigger, Action<IIrcMessage> callback)
        {
            RegisterTrigger(trigger, callback, false);
        }

        public void RegisterTrigger(string trigger, Action<IIrcMessage> callback, bool needChannel)
        {
            if (trigger == null)
                throw new ArgumentNullException(nameof(trigger));
            if (trigger == string.Empty)
                throw new ArgumentException("Cannot be an empty string.", nameof(trigger));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            triggers.RegisterTrigger(trigger, new Trigger(callback, needChannel));
        }

        public void FireTrigger(IrcMessage msg)
        {
            triggers.FireTrigger(msg);
        }


        public string ConfPathTo(string filename)
        {
            return Path.Combine(ConfDir, filename);
        }
        public string DataPathTo(string filename)
        {
            return Path.Combine(DataDir, filename);
        }


        public void LoadAndWatchConfig<T>(string filename, XmlConfig2<T> xmlConf)
        {
            if (xmlConf == null)
                throw new ArgumentNullException(nameof(xmlConf));
            
            LoadAndWatchConfig(filename, xmlConf.LoadConfig);
        }

        public void LoadAndWatchConfig(string filename, Action<string> loadConfig)
        {
            if (filename == null)
                throw new ArgumentNullException(nameof(filename));
            if (loadConfig == null)
                throw new ArgumentNullException(nameof(loadConfig));
            if (filename.Trim() == string.Empty)
                throw new ArgumentException("Cannot be empty or whitespace.", nameof(filename));
            
            watcher.LoadAndWatch(filename, loadConfig);
        }


        public bool Auth(string nick, string pass)
        {
            if (nick == null)
                throw new ArgumentNullException(nameof(nick));

            return userAuths.Authenticate(nick, pass);
        }

        public int AuthLevel(string nick)
        {
            if (nick == null)
                throw new ArgumentNullException(nameof(nick));

            return userAuths.AuthLevel(nick);
        }
    }
}

