using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
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
        IrcComm ircComm;

        string nick;
        string[] channels;


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
            plugins.LoadPlugins(ircComm);
            // Print number and descriptions of loaded plugins.
            Console.WriteLine("Done! Loaded {0} plugin(s):", plugins.Count);
            foreach (string s in plugins.GetDescriptions())
                Console.WriteLine("- " + s);
        }

        // Constructor
        public Meido(string nick, string server, int port, string[] channels)
        {
            // Assign parameters we'll need in other methods to fields.
            this.channels = channels;
            this.nick = nick;

            // Setting some SmartIrc4Net options.
            irc.CtcpVersion = "MeidoBot v0.87.7";
            irc.AutoJoinOnInvite = true;
            irc.Encoding = Encoding.UTF8;
            irc.ActiveChannelSyncing = true;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;

            // Make sure they know my greatness.
            Console.WriteLine("Starting {0}, written by Ivion.", irc.CtcpVersion);

            // Initiliaze the IrcComm with our IrcClient instance.
            ircComm = new IrcComm(irc);

            LoadPlugins();

            // Add our methods (defined above) to handle IRC events.
            irc.OnConnected += new EventHandler(OnConnected);
            irc.OnChannelMessage += new IrcEventHandler(OnChannelMessage);

            // Try to connect to server.
            Console.WriteLine("\nTrying to connect to {0}:{1} ...", server, port);
            try
            {
                irc.Connect(server, port);
            }
            catch (CouldNotConnectException ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Main.exe <nick> <server>:<port> [#channel1,#channel2,...]");
                return;
            }

            string[] serverAndPort = args[1].Split(':');

            string[] channels = {};
            if (args.Length >= 3)
                channels = args[2].Split(',');

            new Meido(args[0], serverAndPort[0], Convert.ToInt32(serverAndPort[1]), channels);
        }
    }
}