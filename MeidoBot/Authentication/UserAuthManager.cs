using System;
using System.Collections.Generic;
using System.Xml.Linq;
using MeidoCommon;


namespace MeidoBot
{
    class UserAuthManager
    {
        readonly Logger log;

        volatile Dictionary<string, UserAuth> auths =
            new Dictionary<string, UserAuth>(StringComparer.OrdinalIgnoreCase);

        static readonly XElement defaultConfig =
            new XElement("auth",
                new XElement("entry",
                    new XElement("nick"),
                    new XElement("level"),
                    new XElement("pass")
                )
            );


        public UserAuthManager(string filename, WatchConfig watcher, Logger log)
        {
            this.log = log;
            var xmlConf = new XmlConfig2< Dictionary<string, UserAuth> >(
                defaultConfig,
                Parse,
                log,
                Configure
            );
            watcher.LoadAndWatch(filename, xmlConf.LoadConfig);
        }

        void Configure(Dictionary<string, UserAuth> authDict)
        {
            auths = authDict;
        }


        Dictionary<string, UserAuth> Parse(XElement xml)
        {
            var authDict = new Dictionary<string, UserAuth>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement entry in xml.Elements("entry"))
            {
                ParseEntry(entry, authDict);
            }

            return authDict;
        }

        void ParseEntry(XElement entry, Dictionary<string, UserAuth> dict)
        {
            var pass = (string)entry.Element("pass");
            int level = ParseLevel(entry.Element("level"));

            foreach (XElement xnick in entry.Elements("nick"))
            {
                var nick = (string)xnick;

                if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(pass))
                {
                    var user = new UserAuth(pass, level);

                    log.Verbose("Registering user '{0}' with level {1}.", nick, user.Level);
                    dict[nick] = user;
                }
            }
        }

        static int ParseLevel(XElement el)
        {
            int level;
            if (el != null && int.TryParse(el.Value, out level))
                return level;
            else
                return 0;
        }


        public bool Authenticate(string nick, string pass)
        {
            UserAuth user;
            bool success = false;
            if (auths.TryGetValue(nick, out user))
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
            if (auths.TryGetValue(nick, out user))
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