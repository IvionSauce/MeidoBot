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
        public Action<IIrcMessage> TriggerHandlers { get; private set; }


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
        public void AddTriggerHandler(Action<IIrcMessage> handler)
        {
            TriggerHandlers += handler;
        }


        public void SendMessage(string target, string message, params object[] args)
        {
            SendMessage( target, string.Format(message, args) );
        }

        public void SendMessage(string target, string message)
        {
            irc.SendMessage(SendType.Message, target, message);
        }


        public void DoAction(string target, string action, params object[] args)
        {
            DoAction( target, string.Format(action, args) );
        }

        public void DoAction(string target, string action)
        {
            irc.SendMessage(SendType.Action, target, action);
        }


        public void SendNotice(string target, string message, params object[] args)
        {
            SendNotice( target, string.Format(message, args) );
        }

        public void SendNotice(string target, string message)
        {
            irc.SendMessage(SendType.Notice, target, message);
        }


        public string[] GetChannels()
        {
            return irc.GetChannels();
        }
        public bool IsMe(string nick)
        {
            return irc.IsMe(nick);
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

        public string Trigger { get; private set; }

        readonly IrcClient irc;
        readonly ReceiveType type;


        public IrcMessage(Meebey.SmartIrc4net.IrcMessageData messageData, string prefix)
        {
            irc = messageData.Irc;
            type = messageData.Type;

            Message = messageData.Message;
            MessageArray = messageData.MessageArray;
            Channel = messageData.Channel;
            Nick = messageData.Nick;
            Ident = messageData.Ident;
            Host = messageData.Host;

            Trigger = ParseTrigger(prefix);
        }


        // Returns trigger without the prefix. Will be null if message didn't start with a prefix.
        // Will be empty if the prefix was called without a subsequent trigger.
        // In case of a query message it will contain the first word, even if it didn't start with the prefix.
        string ParseTrigger(string prefix)
        {
            if (Message.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (MessageArray[0].Length == prefix.Length)
                    return string.Empty;
                else
                    return MessageArray[0].Substring(prefix.Length);
            }
            else if (type == ReceiveType.QueryMessage)
                return MessageArray[0];
            else
                return null;
        }


        public void Reply(string message, params object[] args)
        {
            Reply( string.Format(message, args) );
        }

        public void Reply(string message)
        {
            switch(type)
            {
            case ReceiveType.ChannelMessage:
            case ReceiveType.ChannelAction:
                irc.SendMessage(SendType.Message, Channel, string.Concat(Nick, ": ", message));
                return;
            case ReceiveType.QueryMessage:
            case ReceiveType.QueryAction:
                irc.SendMessage(SendType.Message, Nick, message);
                return;
            default:
                throw new InvalidOperationException("Unexpected ReceiveType.");
            }
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
                // This somehow doesn't end the main thread. Just another episode in "shit I don't get".
                irc.Disconnect();
                // Maybe this will make the main thread exit...
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


        void OnMessage(object sender, IrcEventArgs e)
        {
            var msg = new IrcMessage(e.Data, plugins.Prefix);

            if (msg.Trigger != null && ircComm.TriggerHandlers != null)
                ircComm.TriggerHandlers(msg);
            else if (string.IsNullOrEmpty(msg.Channel))
                QueryMessage(msg);
            else
                ChannelMessage(msg);
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
            irc.CtcpVersion = "MeidoBot v0.88.0";
            irc.AutoJoinOnInvite = true;
            irc.Encoding = Encoding.UTF8;
            irc.ActiveChannelSyncing = true;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;

            // Make sure they know my greatness.
            Console.WriteLine("Starting {0}, written by Ivion.", irc.CtcpVersion);

            plugins.Prefix = prefix;
            // Initialize the IrcComm with our IrcClient instance.
            ircComm = new IrcComm(irc);
            LoadPlugins();

            // Add our methods (defined above) to handle IRC events.
            irc.OnConnected += new EventHandler(OnConnected);
            irc.OnChannelMessage += new IrcEventHandler(OnMessage);
            irc.OnQueryMessage += new IrcEventHandler(OnMessage);
        }


        public void Dispose()
        {
            irc.Disconnect();
            plugins.StopPlugins();
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