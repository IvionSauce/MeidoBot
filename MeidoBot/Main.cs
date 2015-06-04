using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;


namespace MeidoBot
{
    static class Program
    {
        public static readonly string Version = "0.89.4";

        const string ExampleConfig = @"
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
</config>
";

        static void Main(string[] args)
        {
            const string abort = "!! Aborting.";
            const string miniHelp = "MeidoBot.exe <config.xml>";

            if (args.Length != 1)
            {
                Console.WriteLine(miniHelp);
                return;
            }
            else if (args[0].Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(miniHelp);
                Console.WriteLine("Example config:" + ExampleConfig);
                Console.WriteLine("Modify and save to a file. Pass path as argument to MeidoBot.");
                return;
            }


            XElement xmlConfig;
            string file = args[0];
            try
            {
                xmlConfig = XElement.Load(file);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                    Console.WriteLine("!! Could not find '{0}'.", file);
                else if (ex is UnauthorizedAccessException)
                    Console.WriteLine("!! Access to '{0}' is denied.", file);
                else if (ex is XmlException)
                    Console.WriteLine("!! Error parsing XML: " + ex.Message);
                else
                    throw;

                Console.WriteLine(abort);
                return;
            }


            MeidoConfig meidoConfig;
            var result = Parsing.ParseConfig(xmlConfig, out meidoConfig);

            switch (result)
            {
            case Parsing.Results.Success:
                Console.WriteLine("Starting MeidoBot {0}\n", Version);
                Ssl.EnableTrustAll();
                new Meido(meidoConfig);
                return;
                
            // Error reporting.
            case Parsing.Results.NoServer:
                Console.WriteLine("Please set a server address for the bot to connect to.");
                break;
            case Parsing.Results.NoNickname:
                Console.WriteLine("Please set a nickname for the bot.");
                break;
            case Parsing.Results.InvalidPortNumber:
                Console.WriteLine("Given port number was invalid.");
                break;
            case Parsing.Results.TriggerWhitespace:
                Console.WriteLine("Trigger prefix cannot contain whitespace.");
                break;
            default:
                Console.WriteLine("Unknown error occurred! (This should not happen)");
                break;
            }
            Console.WriteLine(abort);
        }
    }
}