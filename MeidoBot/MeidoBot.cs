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


    // Implement IIrcComm by using a IrcClient as backend, allowing a limited subset of its methods to be used by
    // the plugins.
    class IrcComm : IIrcComm
    {
        IrcClient irc;
        public Action<IIrcMessage> ChannelMessageHandlers { get; private set; }
        public Action<IIrcMessage> QueryMessageHandlers { get; private set; }


        public IrcComm(IrcClient ircClient)
        {
            irc = ircClient;
        }

        public void AddChannelMessageHandler(Action<IIrcMessage> handler)
        {
            ChannelMessageHandlers += handler;
        }
        public void AddQueryMessageHandler(Action<IIrcMessage> handler)
        {
            QueryMessageHandlers += handler;
        }

        public void SendMessage(string target, string message)
        {
            irc.SendMessage(SendType.Message, target, message);
        }
        public void DoAction(string target, string action)
        {
            irc.SendMessage(SendType.Action, target, action);
        }
        public void SendNotice(string target, string message)
        {
            irc.SendMessage(SendType.Notice, target, message);
        }

        public string[] GetChannels()
        {
            return irc.GetChannels();
        }
    }


    // Implement IIrcMessage and supply a constructor that fills in the fields, once again allowing the subset of
    // a SmartIrc4Net class to trickle down to the plugins.
    class IrcMessage : IIrcMessage
    {
        public string Message { get; private set; }
        public string[] MessageArray { get; private set; }
        public string Channel { get; private set; }
        public string Nick { get; private set; }
        public string Ident { get; private set; }
        public string Host { get; private set; }


        // Constructor
        public IrcMessage(Meebey.SmartIrc4net.IrcMessageData messageData)
        {
            Message = messageData.Message;
            MessageArray = messageData.MessageArray;
            Channel = messageData.Channel;
            Nick = messageData.Nick;
            Ident = messageData.Ident;
            Host = messageData.Host;
        }
    }


    class Meido
    {
        IrcClient irc = new Meebey.SmartIrc4net.IrcClient();
        PluginManager plugins = new PluginManager();

        // IRC Communication object to be passed along to the plugins, so they can respond freely through it.
        // Also used to call the relevant method(s) on receiving messages.
        IrcComm ircComm;

        string nick;
        string[] channels;

        // public MeidoComm MeidoComm { get; private set; }


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
                Console.WriteLine(ex.Message);
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

        // Pass on the message and associated info to the plugins.
        void OnChannelMessage(object sender, Meebey.SmartIrc4net.IrcEventArgs e)
        {
            if (e.Data.MessageArray[0] == plugins.Prefix + "h" ||
                e.Data.MessageArray[0] == plugins.Prefix + "help")
            {
                string helpMessage = Help(e.Data.MessageArray);
                irc.SendMessage(SendType.Message, e.Data.Channel, helpMessage);
            }
            // Only dispatch to the plugins if there's something to dispatch.
            else if (e.Data.MessageArray.Length > 0)
                ircComm.ChannelMessageHandlers(new IrcMessage(e.Data));

        }

        // Help trigger
        string Help(string[] message)
        {
            if (message.Length == 1)
            {
                string[] keys = plugins.GetHelpSubjects();
                var subjects = string.Join(", ", keys);

                return "Help is available on the following subjects: " + subjects;
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

        void LoadPlugins()
        {
            // Load plugins and announce we're doing so.
            Console.WriteLine("Loading plugins...");
            plugins.LoadPlugins( ircComm, new MeidoComm() );
            // Print number and descriptions of loaded plugins.
            Console.WriteLine("Done! Loaded {0} plugin(s):", plugins.Count);
            foreach (string s in plugins.GetDescriptions())
                Console.WriteLine("- " + s);
        }

        public Meido(string nick, string prefix)
        {
            this.nick = nick;

            // Setting some SmartIrc4Net options.
            irc.CtcpVersion = "MeidoBot v0.87.8";
            irc.AutoJoinOnInvite = true;
            irc.Encoding = Encoding.UTF8;
            irc.ActiveChannelSyncing = true;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;

            // Make sure they know my greatness.
            Console.WriteLine("Starting {0}, written by Ivion.", irc.CtcpVersion);

            plugins.Prefix = prefix;
            // Initiliaze the IrcComm with our IrcClient instance.
            ircComm = new IrcComm(irc);
            LoadPlugins();

            // Add our methods (defined above) to handle IRC events.
            irc.OnConnected += new EventHandler(OnConnected);
            irc.OnChannelMessage += new IrcEventHandler(OnChannelMessage);
        }
    }


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
            // Allow port to have the default of 6667.
            int port = (int?)config.Element("port") ?? 6667;

            string[] channels = ParseChannels(config);

            // Default prefix for triggers is "."
            var triggerPrefix = (string)config.Element("trigger-prefix") ?? ".";
            // Default configuration directory is the base directory of the application, since we know for sure that
            // that directory exists.
            // var confDir = (string)config.Element("conf-directory") ?? AppDomain.CurrentDomain.BaseDirectory;

            // Finally active the Meido.
            var meido = new Meido(nick, triggerPrefix);
            meido.Connect(server, port, channels);
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