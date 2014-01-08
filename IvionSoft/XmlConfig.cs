using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;


namespace IvionSoft
{
    public abstract class XmlConfig
    {
        public XElement Config { get; private set; }
        
        public XmlConfig(string file)
        {
            try
            {
                Config = XElement.Load(file);
                Console.WriteLine("-> Loaded config from " + file);
            }
            catch (FileNotFoundException)
            {
                Config = DefaultConfig();
                Config.Save(file);
                Console.WriteLine("-> Created default config at " + file);
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine(" ! Directory not found: " + file);
                Console.WriteLine("-> Loading default config.");
                Config = DefaultConfig();
            }
            catch (XmlException ex)
            {
                Console.WriteLine("!! XML Exception: " + ex.Message);
                Console.WriteLine("-> Loading default config.");
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
                    Console.WriteLine(" ! Error(s) in loading values from {0}. ({1})", file, ex.Message);
                    Console.WriteLine("-> Loading default config.");
                    Config = DefaultConfig();
                    LoadConfig();
                }
                else
                    throw;
            }
        }
        
        public abstract void LoadConfig();
        public abstract XElement DefaultConfig();
    }
}