using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;


namespace MeidoCommon
{
    public class XmlConfig2<T>
    {
        public delegate T XmlParser(XElement xml);

        readonly XElement defaultConfig;
        readonly XmlParser parser;
        readonly ILog log;

        readonly object _locker = new object();
        Action<T> OnConfigChange;


        public XmlConfig2(XElement defaultConfig, XmlParser parser, ILog log)
        {
            if (defaultConfig == null)
                throw new ArgumentNullException(nameof(defaultConfig));
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            
            this.defaultConfig = defaultConfig;
            this.parser = parser;
            this.log = log;
        }

        public XmlConfig2(
            XElement defaultConfig,
            XmlParser parser,
            ILog log,
            params Action<T>[] configCallbacks) : this(defaultConfig, parser, log)
        {
            AddCallbacks(configCallbacks);
        }


        public void AddCallbacks(params Action<T>[] configCallbacks)
        {
            if (configCallbacks == null)
                throw new ArgumentNullException(nameof(configCallbacks));
            
            lock (_locker)
            {
                foreach (var cb in configCallbacks)
                {
                    OnConfigChange += cb;
                }
            }
        }


        public void LoadConfig(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path.Trim() == string.Empty)
                throw new ArgumentException("Cannot be empty or whitespace.", nameof(path));

            lock (_locker)
            {
                if (OnConfigChange != null)
                {
                    T parsedConf = DWIM(path, defaultConfig, parser, log);
                    OnConfigChange(parsedConf);
                }
            }
        }


        public static T DWIM(
            string path,
            XElement defaultConfig,
            XmlParser parser,
            ILog log)
        {
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));
            // GetOrCreateConfig checks the other parameters.
            var xmlConfig = XmlConfig.GetOrCreateConfig(path, defaultConfig, log);

            T config;
            if (!TryParseConfig(xmlConfig, path, parser, log, out config))
            {
                log.Message("-> Loading values from default config.");
                config = parser(defaultConfig);
            }

            return config;
        }

        static bool TryParseConfig(
            XElement config,
            string configPath,
            XmlParser parser,
            ILog log,
            out T parsedConf)
        {
            try
            {
                parsedConf = parser(config);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is FormatException || ex is NullReferenceException)
                {
                    log.Error("Error(s) in loading values from {0} ({1})", configPath, ex.Message);
                    parsedConf = default(T);
                    return false;
                }
                else
                    throw;
            }
        }
    }
}