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
            this.defaultConfig = defaultConfig;
            this.parser = parser;
            this.log = log;
        }


        public void AddCallbacks(params Action<T>[] callbacks)
        {
            if (callbacks == null)
                throw new ArgumentNullException(nameof(callbacks));
            
            lock (_locker)
            {
                foreach (var cb in callbacks)
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
            var xmlConfig = XmlConfig.GetOrCreateConfig(path, defaultConfig, log);

            T config;
            if (TryParseConfig(xmlConfig, path, parser, log, out config))
            {
                return config;
            }
            else
            {
                log.Message("-> Loading values from default config.");
                config = parser(defaultConfig);
                return config;
            }
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