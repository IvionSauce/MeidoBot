using MeidoCommon;


namespace MeidoBot
{
    class UserAuthManager
    {
        readonly Logger log;
        volatile AuthDictionary auths;


        public UserAuthManager(string filename, WatchConfig watcher, Logger log)
        {
            this.log = log;
            // Setting up configuration.
            var xmlConf = new XmlConfig2<AuthDictionary>(
                AuthDictionary.DefaultConfig(),
                (xml) => new AuthDictionary(xml),
                log,
                Configure
            );
            watcher.LoadAndWatch(filename, xmlConf.LoadConfig);
        }

        void Configure(AuthDictionary dict)
        {
            auths = dict;
            foreach (var pair in auths)
            {
                log.Verbose("Registering user '{0}' with level {1}.", pair.Key, pair.Value.Level);
            }
        }


        public bool Authenticate(string nick, string pass)
        {
            UserAuth user;
            bool success = false;
            if (auths.TryGet(nick, out user))
                success = user.Authenticate(pass);
                
            if (success)
                log.Message("Successful authentication by {0}.", nick);
            else
                log.Message("Attempted authentication by {0}.", nick);

            return success;
        }

        public int AuthLevel(string nick)
        {
            UserAuth user;
            if (auths.TryGet(nick, out user))
            {
                /* Ideally I'd want a Authentication system that could leverage the NickServ registration already
                 * in place on many IRC servers. Sadly SmartIrc4Net's IrcUser.IsRegistered doesn't update the
                 * registered status on nick change, when a nick that was indeed registered with NickServ changes to a
                 * nick that isn't registered it still reports that it is. This is of course an unworkable basis for
                 * an Authentication system. (It did detect the changes on a part/join) */
                if (user.IsAuthenticated)
                    return user.Level;
            }
            return 0;
        }
    }
}