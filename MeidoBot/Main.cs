using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;


namespace MeidoBot
{
    static class Program
    {
        public static readonly string Version = "0.90.0";


        const string abort = "!! Aborting.";
        const string miniHelp = "MeidoBot.exe <config.xml>";

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
  <conf-dir><!--Directory where configuration files will be stored--></conf-dir>
  <data-dir><!--Directory where data files will be stored--></data-dir>
</config>
";

        static string configPath;
        static Meido bot;


        static void Main(string[] args)
        {
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

            configPath = args[0];
            bot = CreateMeido();
            bot.Connect();
        }


        public static void RestartMeido()
        {
            var newbot = CreateMeido();
            if (newbot != null)
            {
                bot.Disconnect("Restarting...");
                bot.Dispose();

                bot = newbot;
                GC.Collect();
                bot.Connect();
            }
        }


        static Meido CreateMeido()
        {
            XElement xmlConfig;
            try
            {
                xmlConfig = XElement.Load(configPath);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                    Console.WriteLine("!! Could not find '{0}'.", configPath);
                else if (ex is UnauthorizedAccessException)
                    Console.WriteLine("!! Access to '{0}' is denied.", configPath);
                else if (ex is XmlException)
                    Console.WriteLine("!! Error parsing XML: " + ex.Message);
                else
                    throw;

                Console.WriteLine(abort);
                return null;
            }

            return CreateMeido(xmlConfig);
        }

        static Meido CreateMeido(XElement xmlConfig)
        {
            MeidoConfig meidoConfig;
            var result = Parsing.ParseConfig(xmlConfig, out meidoConfig);

            switch (result)
            {
            case Parsing.Result.Success:
                Console.WriteLine("Starting MeidoBot {0}\n", Version);
                Ssl.EnableTrustAll();
                return new Meido(meidoConfig);

                // Error reporting.
            case Parsing.Result.NoServer:
                Console.WriteLine("Please set a server address for the bot to connect to.");
                break;
            case Parsing.Result.NoNickname:
                Console.WriteLine("Please set a nickname for the bot.");
                break;
            case Parsing.Result.InvalidPortNumber:
                Console.WriteLine("Given port number was invalid.");
                break;
            case Parsing.Result.TriggerWhitespace:
                Console.WriteLine("Trigger prefix cannot contain whitespace.");
                break;
            default:
                Console.WriteLine("Unknown error occurred! (This should not happen)");
                break;
            }
            Console.WriteLine(abort);
            return null;
        }
    }
}