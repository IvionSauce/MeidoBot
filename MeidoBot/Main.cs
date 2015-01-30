using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;


namespace MeidoBot
{
    // Entry point, also parses the XML config.
    // Ugly shit, will need to be refactored... someday.
    static class Program
    {
        public static readonly string Version = "0.89.0";

        static void Main(string[] args)
        {
            const string abort = "!! Aborting.";

            if (args.Length != 1)
            {
                Console.WriteLine("MeidoBot.exe <config.xml>");
                return;
            }
            
            XElement config = GetXmlConfig(args[0]);
            // Null means failure, abort.
            if (config == null)
            {
                Console.WriteLine(abort);
                return;
            }
            
            // Load the settings into variables.
            var nick = (string)config.Element("nick");
            var server = (string)config.Element("server");
            if (string.IsNullOrWhiteSpace(nick) || string.IsNullOrWhiteSpace(server))
            {
                Console.WriteLine("!! You will need to set both a nickname and a server to connect to.");
                Console.WriteLine(abort);
                return;
            }
            var prefix = ParsePrefix(config);
            if (prefix == null)
            {
                Console.WriteLine("!! Trigger prefix can't contain whitespace.");
                Console.WriteLine(abort);
                return;
            }
            
            // Allow port to have the default of 6667.
            int port = (int?)config.Element("port") ?? 6667;
            string[] channels = ParseChannels(config);
            
            // Finally activate the Meido.
            Console.WriteLine("Starting MeidoBot {0}\n", Version);
            new Meido(server, port, nick, channels, prefix);
        }
        
        
        static string ParsePrefix(XElement config)
        {
            var triggerPrefix = (string)config.Element("trigger-prefix");
            // Default prefix for triggers is "."
            if (string.IsNullOrEmpty(triggerPrefix))
                return ".";
            
            // Reject a prefix when it contains whitespace.
            foreach (char c in triggerPrefix)
                if (char.IsWhiteSpace(c))
                    return null;
            
            return triggerPrefix;
        }
        
        
        static string[] ParseChannels(XElement config)
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
            
            return chanList.ToArray();
        }
        
        
        static XElement GetXmlConfig(string file)
        {
            XElement config = null;
            try
            {
                config = XElement.Load(file);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                    Console.WriteLine("!! Could not find " + file);
                else
                    throw;
            }
            return config;
        }
    }
}