using System;
using System.Collections.Generic;
using System.Xml.Linq;
using MeidoCommon;
using Meebey.SmartIrc4net;


namespace MeidoBot
{
    class UserAuthManager
    {
        readonly Logger log;

        readonly Dictionary<string, UserAuth> auths =
            new Dictionary<string, UserAuth>(StringComparer.OrdinalIgnoreCase);

        static readonly XElement defaultConfig =
            new XElement("auth",
                new XElement("entry",
                    new XElement("nick"),
                    new XElement("level"),
                    new XElement("pass")
                )
            );


        public UserAuthManager(string path, Logger log)
        {
            this.log = log;

            XElement authConfig = XmlConfig.GetOrCreateConfig(path, defaultConfig, log);

            foreach (XElement entry in authConfig.Elements("entry"))             
                ParseEntry(entry);
        }

        void ParseEntry(XElement entry)
        {
            var nick = (string)entry.Element("nick");
            var pass = (string)entry.Element("pass");
            int level = ParseLevel(entry.Element("level"));

            if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(pass))
            {
                log.Verbose("Registering user '{0}' with level {1}.", nick, level);
                auths[nick] = new UserAuth(pass, level);
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
                if (user.IsAuthenticated)
                    return user.Level;
            }
            return 0;
        }
    }


    class UserAuth
    {
        public readonly int Level;

        readonly string password;

        bool authenticated;
        DateTimeOffset authTime;
        static readonly TimeSpan maxTime = TimeSpan.FromMinutes(10);
        object _authLock = new object();

        public bool IsAuthenticated
        {
            get
            {
                lock (_authLock)
                {
                    if (DateTimeOffset.Now - authTime < maxTime)
                        return authenticated;
                    else
                        return false;
                }
            }
        }


        public UserAuth(string pass, int lvl)
        {
            if (string.IsNullOrWhiteSpace(pass))
                password = string.Empty;
            else
                password = pass;

            const int maxLvl = 10;
            if (lvl > maxLvl)
                Level = maxLvl;
            else if (lvl < 0)
                Level = 0;
            else
                Level = lvl;
        }


        public bool Authenticate(string pass)
        {
            if (password.Equals(pass, StringComparison.Ordinal))
            {
                lock (_authLock)
                {
                    authenticated = true;
                    authTime = DateTimeOffset.Now;
                    return true;
                }
            }
            else
                return false;
        }

    }
}