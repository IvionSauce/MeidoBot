using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;

namespace MeidoBot
{
    class AuthDictionary : IEnumerable<KeyValuePair<string, UserAuth>>
    {
        readonly Dictionary<string, UserAuth> auths;


        public AuthDictionary(XElement xml)
        {
            auths = new Dictionary<string, UserAuth>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement entry in xml.Elements("entry"))
            {
                ParseEntry(entry);
            }
        }

        void ParseEntry(XElement entry)
        {
            var pass = (string)entry.Element("pass");
            int level = ParseLevel(entry.Element("level"));

            foreach (XElement xnick in entry.Elements("nick"))
            {
                var nick = (string)xnick;

                if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrWhiteSpace(pass))
                {
                    auths[nick] = new UserAuth(pass, level);
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


        public bool TryGet(string nick, out UserAuth user)
        {
            return auths.TryGetValue(nick, out user);
        }


        public static XElement DefaultConfig()
        {
            var config =
                new XElement("auth",
                             new XElement("entry",
                                          new XElement("nick"),
                                          new XElement("level"),
                                          new XElement("pass")
                                         )
                            );

            return config;
        }


        public IEnumerator<KeyValuePair<string, UserAuth>> GetEnumerator()
        {
            return auths.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}