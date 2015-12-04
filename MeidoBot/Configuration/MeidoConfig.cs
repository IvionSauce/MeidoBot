using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace MeidoBot
{
    public class MeidoConfig
    {
        public readonly string Nickname;
        public readonly string ServerAddress;
        public readonly int ServerPort;
        public readonly List<string> Channels;
        public readonly string TriggerPrefix;

        public const int DefaultPort = 6667;


        string basePath;
        string _confDir;
        public string ConfigurationDirectory
        {
            get { return _confDir; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    _confDir = Path.Combine(basePath, "conf");
                else
                    _confDir = value;
            }
        }

        string _dataDir;
        public string DataDirectory
        {
            get { return _dataDir; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    _dataDir = Path.Combine(basePath, "data");
                else
                    _dataDir = value;
            }
        }


        public MeidoConfig(string nickname, string address, string triggerPrefix) :
        this(nickname, address, DefaultPort, new string[0], triggerPrefix) {}

        public MeidoConfig(string nickname, string address, int port, string triggerPrefix) :
        this(nickname, address, port, new string[0], triggerPrefix) {}

        public MeidoConfig(string nickname, string address, IEnumerable<string> channels, string triggerPrefix) :
        this(nickname, address, DefaultPort, channels, triggerPrefix) {}

        // So much argument checking, it's almost sickening...
        public MeidoConfig(string nickname, string address, int port,
            IEnumerable<string> channels, string triggerPrefix)
        {
            // Null checking.
            if (nickname == null)
                throw new ArgumentNullException("nickname");
            else if (address == null)
                throw new ArgumentNullException("address");
            else if (channels == null)
                throw new ArgumentNullException("channels");
            else if (triggerPrefix == null)
                throw new ArgumentNullException("triggerPrefix");
            // Empty checking.
            else if (triggerPrefix == string.Empty)
                throw new ArgumentException("Cannot be empty.", "triggerPrefix");
            // Checking if port number is in valid range.
            else if (!IsValidPortNumber(port))
                throw new ArgumentOutOfRangeException("port",
                    "Port number must be a value between 1 and 65535 (inclusive).");


            Nickname = nickname.Trim();
            ServerAddress = address.Trim();
            ServerPort = port;
            Channels = channels.ToList();
            TriggerPrefix = triggerPrefix;

            // Whitespace checking.
            if (Nickname == string.Empty)
                throw new ArgumentException("Cannot be empty or whitespace.", "nickname");
            else if (ServerAddress == string.Empty)
                throw new ArgumentException("Cannot be empty or whitespace.", "address");
            else if (!IsValidTriggerPrefix(triggerPrefix))
                throw new ArgumentException("Cannot contain whitespace characters.", "triggerPrefix");

            SetBasePath(GetDefaultBasePath());
        }


        public static bool IsValidTriggerPrefix(string prefix)
        {
            if (prefix == null || prefix == string.Empty)
                return false;

            foreach (char c in prefix)
            {
                if (char.IsWhiteSpace(c))
                    return false;
            }
            return true;
        }

        public static bool IsValidPortNumber(int port)
        {
            if (port > 0 && port <= 65535)
                return true;
            else
                return false;
        }


        public void SetBasePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Cannot be null, empty or whitespace", "path");

            basePath = path;
            ConfigurationDirectory = null;
            DataDirectory = null;
        }


        public string GetDefaultBasePath()
        {
            string homePath;
            string meidoDir;
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                homePath = Environment.GetEnvironmentVariable("HOME");
                meidoDir = ".meidobot";
            }
            else
            {
                homePath = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
                meidoDir = "meidobot";
            }
            var serverDir = ServerAddress.Replace('.', '-');

            return Path.Combine(homePath, meidoDir, serverDir);
        }
    }
}