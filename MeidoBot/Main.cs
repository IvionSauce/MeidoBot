using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;


namespace MeidoBot
{
    static class Program
    {
        public static readonly string Version = "0.96.5";

        const string miniHelp = "MeidoBot.exe <config.xml>";


        static string configPath;
        static Meido bot;


        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine(miniHelp);
                return;
            }
            if (args[0].Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(miniHelp);
                Console.WriteLine("Example config:" + Parsing.ExampleConfig);
                Console.WriteLine("Modify and save to a file. Pass path as argument to MeidoBot.");
                return;
            }

            configPath = args[0];
            bot = CreateMeido(Logger.ConsoleLogger("Main"));
            if (bot != null)
            {
                // Holy scoping, Batman!
                System.Threading.Thread.CurrentThread.Name = "SecretlySkynet";
                bot.Connect();
            }
        }


        public static void RestartMeido()
        {
            // TODO: Make the caller supply the logger.
            var log = Logger.ConsoleLogger("Main");
            log.Message("Attempting to restart meido...");

            var newbot = CreateMeido(log);
            if (newbot != null)
            {
                bot.Disconnect("Restarting...");
                // Dispose of old bot and assign new bot.
                bot.Dispose();
                bot = newbot;
                // The previous 2 actions (disposing the old bot and removing the reference to it) have changed
                // the entire program. The bot is the root of a large tree of objects (several helper classes,
                // plugins, etc.), so now is a good time to force a complete collection.
                Collect();
                bot.Connect();
            }
            else
                log.Error("Restart failed.");
        }

        static void Collect()
        {
            // Collect all the memories.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }


        static Meido CreateMeido(Logger log)
        {
            XElement xmlConfig;
            try
            {
                xmlConfig = XElement.Load(configPath);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                    log.Error("Could not find '{0}'.", configPath);
                else if (ex is UnauthorizedAccessException)
                    log.Error("Access to '{0}' is denied.", configPath);
                else if (ex is XmlException)
                    log.Error("Error parsing XML: " + ex.Message);
                else
                    throw;
                
                return null;
            }

            return CreateMeido(xmlConfig, log);
        }

        static Meido CreateMeido(XElement xmlConfig, Logger log)
        {
            MeidoConfig meidoConfig;
            var result = Parsing.ParseConfig(xmlConfig, out meidoConfig);

            Action<string> report =
                errorMsg => log.Error("Missing or incorrect settings in '{0}': {1}", configPath, errorMsg);

            switch (result)
            {
            case Parsing.Result.Success:
                log.Message("Starting MeidoBot {0}", Version);
                Ssl.EnableTrustAll();
                return new Meido(meidoConfig);

            // Error reporting.
            case Parsing.Result.NoServer:
                report("Please set a server address for the bot to connect to.");
                break;
            case Parsing.Result.NoNickname:
                report("Please set a nickname for the bot.");
                break;
            case Parsing.Result.InvalidPortNumber:
                report("Given port number was invalid.");
                break;
            case Parsing.Result.TriggerWhitespace:
                report("Trigger prefix cannot contain whitespace.");
                break;
            default:
                log.Error("Unknown error occurred! (This should not happen)");
                break;
            }

            return null;
        }
    }
}