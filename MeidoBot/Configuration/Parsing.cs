using System;
using System.Collections.Generic;
using System.Xml.Linq;


namespace MeidoBot
{
    static class Parsing
    {
        public enum Result
        {
            Success,
            NoServer,
            NoNickname,
            TriggerWhitespace,
            InvalidPortNumber
        }

        public const string ExampleConfig = @"
<config>
  <!--Required-->
  <nick>MeidoTest</nick>
  <server>irc.server.address</server>
  
  <!--Optional-->
  <port>6667</port>
  <trigger-prefix>.</trigger-prefix>
  <channels>
    <channel>#your</channel>
    <channel>#channels</channel>
  </channels>
  <conf-dir><!--Directory where configuration files will be stored--></conf-dir>
  <data-dir><!--Directory where data files will be stored--></data-dir>
</config>
";


        public static Result ParseConfig(XElement config, out MeidoConfig meidoconf)
        {
            meidoconf = null;

            // Load the settings into variables.
            var server = (string)config.Element("server");
            if (string.IsNullOrWhiteSpace(server))
                return Result.NoServer;

            var nick = (string)config.Element("nick");
            if (string.IsNullOrWhiteSpace(nick))
                return Result.NoNickname;

            var prefix = ParsePrefix(config);
            if (prefix == null)
                return Result.TriggerWhitespace;

            var port = ParsePort(config);
            if (port < 1)
                return Result.InvalidPortNumber;

            var channels = ParseChannels(config);

            // Construct meidoconf with previously loaded values.
            meidoconf = new MeidoConfig(nick, server, port, channels, prefix);

            // Set optional properties on meidoconfig.
            meidoconf.ConfigurationDirectory = (string)config.Element("conf-dir");
            meidoconf.DataDirectory = (string)config.Element("data-dir");

            return Result.Success;
        }

        static string ParsePrefix(XElement config)
        {
            var triggerPrefix = (string)config.Element("trigger-prefix");
            // Default prefix for triggers is "."
            if (string.IsNullOrEmpty(triggerPrefix))
                return ".";

            if (MeidoConfig.IsValidTriggerPrefix(triggerPrefix))
                return triggerPrefix;
            else
                return null;
        }

        static int ParsePort(XElement config)
        {
            var portStr = (string)config.Element("port");
            // Allow port to have the default of 6667.
            if (string.IsNullOrEmpty(portStr))
                return MeidoConfig.DefaultPort;

            int port;
            if (int.TryParse(portStr, out port))
            {
                if (MeidoConfig.IsValidPortNumber(port))
                    return port;
                else
                    return 0;
            }
            else
                return -1;
        }

        static List<string> ParseChannels(XElement config)
        {
            var chanList = new List<string>();
            // Iterate over the channel entries if they exist and add them to chanList.
            XElement channels = config.Element("channels");
            if (channels != null)
            {
                foreach (XElement channel in channels.Elements())
                {
                    string chan = channel.Value;
                    // Ignore empty entries or those not indicating a channel (with "#").
                    if (!string.IsNullOrEmpty(chan) && chan[0] == '#')
                        chanList.Add(chan);
                }
            }

            return chanList;
        }
    }
}