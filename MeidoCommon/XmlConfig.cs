using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;


namespace MeidoCommon
{
    public abstract class XmlConfig
    {
        public XElement Config { get; private set; }
        
        public XmlConfig(string path, ILog log)
        {
            Config = GetOrCreateConfig(path, DefaultConfig(), log);
            
            try
            {
                LoadConfig();
            }
            catch (Exception ex)
            {
                if (ex is FormatException || ex is NullReferenceException)
                {
                    log.Error("Error(s) in loading values from {0} ({1})", path, ex.Message);
                    log.Message("-> Loading values from default config.");
                    Config = DefaultConfig();
                    LoadConfig();
                }
                else
                    throw;
            }
        }

        public static XElement GetOrCreateConfig(string path, XElement defaultConfig, ILog log)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            else if (log == null)
                throw new ArgumentNullException("log");
            else if (path.Trim() == string.Empty)
                throw new ArgumentException("Cannot be empty or whitespace.", "path");


            try
            {
                var config = XElement.Load(path);
                log.Message("-> Loaded config from " + path);
                return config;
            }
            catch (FileNotFoundException)
            {
                try
                {
                    defaultConfig.Save(path);
                    log.Message("-> Created default config at " + path);
                }
                catch (UnauthorizedAccessException)
                {
                    log.Error("Failed to create default config at " + path);
                }
            }
            catch (DirectoryNotFoundException)
            {
                log.Error("Directory not found: " + path);
            }
            catch (XmlException ex)
            {
                log.Error("XML Exception in loading {0} ({1})", path, ex.Message);
            }

            log.Message("-> Loading default config.");
            return defaultConfig;
        }
        
        public abstract void LoadConfig();
        public abstract XElement DefaultConfig();
    }
}