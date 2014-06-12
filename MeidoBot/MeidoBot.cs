using System;
using System.Text;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
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
            irc.CtcpVersion = "MeidoBot v0.88.4";
            irc.ActiveChannelSyncing = true;
            irc.AutoJoinOnInvite = true;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;
            irc.Encoding = Encoding.UTF8;

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
            
            if (msg.Trigger != null)
            {
                SpecialTriggers(msg);

                if (ircComm.TriggerHandlers != null)
                    ircComm.TriggerHandlers(msg);
            }

            if (msg.Channel != null)
                ChannelMessage(msg);
            else
                QueryMessage(msg);
        }


        void ChannelAction(object sender, IrcEventArgs e)
        {
            if (ircComm.ChannelActionHandlers != null)
                ircComm.ChannelActionHandlers( new IrcMessage(e.Data, plugins.Prefix) );
        }


        void ChannelMessage(IrcMessage msg)
        {
            if (ircComm.ChannelMessageHandlers != null)
                ircComm.ChannelMessageHandlers(msg);
        }

        void QueryMessage(IrcMessage msg)
        {
            if (ircComm.QueryMessageHandlers != null)
                ircComm.QueryMessageHandlers(msg);
        }


        void SpecialTriggers(IIrcMessage msg)
        {
            if (msg.Trigger == "h" || msg.Trigger == "help")
                msg.Reply( Help(msg.MessageArray) );

            else if (msg.Trigger == "auth")
                msg.Reply( Auth(msg.Nick, msg.MessageArray) );

            if (msg.Trigger == "disconnect" && meidoComm.AuthLevel(msg.Nick) == 10)
            {
                // Disconnect from IRC before stopping the plugins, thereby ensuring that once the order to stop has
                // been given no new messages will arrive.
                irc.Disconnect();
                plugins.StopPlugins();
            }
            
            else if (msg.Trigger == "part" &&
                     msg.MessageArray.Length == 2 &&
                     meidoComm.AuthLevel(msg.Nick) == 10)
                irc.RfcPart(msg.MessageArray[1]);
        }


        // Help trigger.
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


        // Auth trigger.
        string Auth(string nick, string[] message)
        {
            if (message.Length > 1)
            {
                if (meidoComm.Auth(nick, message[1]))
                    return "You've successfully authenticated.";
            }

            return "Your current Authentication Level is " + meidoComm.AuthLevel(nick);
        }


        public void Dispose()
        {
            irc.Disconnect();
            plugins.StopPlugins();
        }
    }
}