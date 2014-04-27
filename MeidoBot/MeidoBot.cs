using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Xml.Linq;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    class MeidoComm : IMeidoComm
    {
        public string ConfDir { get; private set; }

        public MeidoComm()
        {
            ConfDir = "conf";
        }
    }


    class Meido : IDisposable
    {
        IrcClient irc = new Meebey.SmartIrc4net.IrcClient();
        PluginManager plugins = new PluginManager();

        // IRC Communication object to be passed along to the plugins, so they can respond freely through it.
        // Also used to call the relevant method(s) on receiving messages.
        IrcComm ircComm;

        string nick;
        string[] channels;

        MeidoComm meidoComm;


        public Meido(string nick, string prefix)
        {
            this.nick = nick;
            
            // Setting some SmartIrc4Net options.
            irc.CtcpVersion = "MeidoBot v0.88.0";
            irc.AutoJoinOnInvite = true;
            irc.Encoding = Encoding.UTF8;
            irc.ActiveChannelSyncing = true;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;
            
            // Make sure they know my greatness.
            Console.WriteLine("Starting {0}, written by Ivion.", irc.CtcpVersion);
            
            plugins.Prefix = prefix;
            LoadPlugins();
            
            // Add our methods (defined above) to handle IRC events.
            irc.OnConnected += new EventHandler(OnConnected);
            irc.OnChannelMessage += new IrcEventHandler(OnMessage);
            irc.OnQueryMessage += new IrcEventHandler(OnMessage);
        }


        void LoadPlugins()
        {
            // Initialize the IrcComm with our IrcClient instance.
            ircComm = new IrcComm(irc);
            meidoComm = new MeidoComm();

            // Load plugins and announce we're doing so.
            Console.WriteLine("Loading plugins...");
            plugins.LoadPlugins(ircComm, meidoComm);
            // Print number and descriptions of loaded plugins.
            Console.WriteLine("Done! Loaded {0} plugin(s):", plugins.Count);
            foreach (string s in plugins.GetDescriptions())
                Console.WriteLine("- " + s);
        }
        

        public void Connect(string server, int port, string[] channels)
        {
            this.channels = channels;

            Console.WriteLine("\nTrying to connect to {0}:{1} ...", server, port);
            try
            {
                irc.Connect(server, port);
            }
            catch (CouldNotConnectException ex)
            {
                Console.WriteLine(ex);
            }
        }


        // Tell the server who we are and join channel(s).
        void OnConnected(object sender, EventArgs e)
        {
            Console.WriteLine("Connection succesful!");
            irc.Login(nick, "Meido Bot", 0, "MeidoBot");
            irc.RfcJoin(channels);
            irc.Listen();
        }


        void OnMessage(object sender, IrcEventArgs e)
        {
            var msg = new IrcMessage(e.Data, plugins.Prefix);
            
            if (msg.Trigger != null && ircComm.TriggerHandlers != null)
                ircComm.TriggerHandlers(msg);

            if (string.IsNullOrEmpty(msg.Channel))
                QueryMessage(msg);
            else
                ChannelMessage(msg);
        }


        // Pass on the message and associated info to the plugins.
        void ChannelMessage(IrcMessage msg)
        {
            if (msg.Trigger == "h" || msg.Trigger == "help")
            {
                string helpMessage = Help(msg.MessageArray);
                msg.Reply(helpMessage);
            }
            else if (ircComm.ChannelMessageHandlers != null)
                ircComm.ChannelMessageHandlers(msg);
        }


        void QueryMessage(IrcMessage msg)
        {
            // Some makeshift stuff, will need to code an authentication system.
            if (msg.Trigger == "disconnect" && msg.Nick == "Ivion")
            {
                // Disconnect from IRC before stopping the plugins, thereby ensuring that once the order to stop has
                // been given no new messages will arrive.
                irc.Disconnect();
                plugins.StopPlugins();
            }

            else if (msg.Trigger == "part" &&
                     msg.MessageArray.Length == 2 &&
                     msg.Nick == "Ivion")
            {
                irc.RfcPart(msg.MessageArray[1]);
            }

            else if (ircComm.QueryMessageHandlers != null)
                ircComm.QueryMessageHandlers(msg);
        }


        // Help trigger
        string Help(string[] message)
        {
            if (message.Length == 1)
            {
                string[] keys = plugins.GetHelpSubjects();
                var subjects = string.Join(", ", keys);

                return "Help is available on - " + subjects;
            }
            else
            {
                string help = plugins.GetHelp( string.Join(" ", message, 1, message.Length - 1) );

                if (help != null)
                    return help;
                else
                    return "No help available.";
            }
        }


        public void Dispose()
        {
            irc.Disconnect();
            plugins.StopPlugins();
        }
    }


    // Entry point, also parses the XML config.
    // Ugly shit, will need to be refactored... someday.
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("MeidoBot.exe <config.xml>");
                return;
            }

            XElement config = GetXmlConfig(args[0]);
            // Null means failure, abort.
            if (config == null)
            {
                Console.WriteLine("!! Aborting.");
                return;
            }

            // Load the settings into variables.
            var nick = (string)config.Element("nick");
            var server = (string)config.Element("server");
            if (string.IsNullOrWhiteSpace(nick) || string.IsNullOrWhiteSpace(server))
            {
                Console.WriteLine("!! You will need to set both a nickname and a server to connect to.");
                Console.WriteLine("!! Aborting.");
                return;
            }
            var prefix = ParsePrefix(config);
            if (prefix == null)
            {
                Console.WriteLine("!! Trigger prefix can't contain whitespace.");
                Console.WriteLine("!! Aborting.");
                return;
            }

            // Allow port to have the default of 6667.
            int port = (int?)config.Element("port") ?? 6667;

            string[] channels = ParseChannels(config);

            // Default configuration directory is the base directory of the application, since we know for sure that
            // that directory exists.
            // var confDir = (string)config.Element("conf-directory") ?? AppDomain.CurrentDomain.BaseDirectory;

            // Finally active the Meido.
            var meido = new Meido(nick, prefix);
            meido.Connect(server, port, channels);
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
                string chan;
                foreach (XElement channel in channels.Elements())
                {
                    chan = channel.Value;
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