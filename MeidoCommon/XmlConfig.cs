using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;


namespace MeidoCommon
{
    public abstract class XmlConfig
    {
        public XElement Config { get; private set; }
        
        public XmlConfig(string file, ILog log)
        {
            try
            {
                Config = XElement.Load(file);
                log.Message("-> Loaded config from " + file);
            }
            catch (FileNotFoundException)
            {
                Config = DefaultConfig();
                Config.Save(file);
                log.Message("-> Created default config at " + file);
            }
            catch (DirectoryNotFoundException)
            {
                log.Error("Directory not found: " + file);
                log.Message("-> Loading default config.");
                Config = DefaultConfig();
            }
            catch (XmlException ex)
            {
                log.Error("XML Exception in loading {0}. ({1})", file, ex.Message);
                log.Message("-> Loading default config.");
                Config = DefaultConfig();
            }
            
            try
            {
                LoadConfig();
            }
            catch (Exception ex)
            {
                if (ex is FormatException || ex is NullReferenceException)
                {
                    log.Error("Error(s) in loading values from {0}. ({1})", file, ex.Message);
                    log.Message("-> Loading values from default config.");
                    Config = DefaultConfig();
                    LoadConfig();
                }
                else
                    throw;
            }
            // Dereference the XML tree, it's not needed after this point and it's just taking up space.
            Config = null;
        }
        
        public abstract void LoadConfig();
        public abstract XElement DefaultConfig();
    }
}