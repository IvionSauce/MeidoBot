using System;
using System.Text;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    class Meido : IDisposable
    {
        readonly IrcClient irc = new Meebey.SmartIrc4net.IrcClient();
        readonly PluginManager plugins;
        readonly Logger log;

        // IRC Communication object to be passed along to the plugins, so they can respond freely through it.
        // Also used to call the relevant method(s) on receiving messages.
        readonly IrcComm ircComm;
        // MeidoBot Communication object, used for functions that concern both the bot-framework and the plugins
        // running in it.
        readonly MeidoComm meidoComm;

        readonly string nick;
        readonly string[] channels;


        public Meido(string server, int port, string nick, string[] channels, string prefix)
        {
            // We need these parameters for events, store them in fields.
            this.nick = nick;
            this.channels = channels;

            // Initialize the IrcComm with the IrcClient for this server/instance.
            ircComm = new IrcComm(irc);

            // Initialize the MeidoComm with the log factory for this server/instance.
            var logFac = new LogFactory(server);
            meidoComm = new MeidoComm(logFac);
            // Set aside some logging for ourself.
            log = logFac.CreateLogger("MEIDO");

            // Setup plugins and load them.
            plugins = new PluginManager(prefix);
            LoadPlugins();
            
            // Setting some SmartIrc4Net options.
            irc.CtcpVersion = "MeidoBot " + Program.Version;
            irc.ActiveChannelSyncing = true;
            irc.AutoJoinOnInvite = true;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;
            irc.Encoding = Encoding.UTF8;

            // Set event handlers and connect to the server.
            irc.OnConnected += new EventHandler(OnConnected);
            irc.OnChannelMessage += new IrcEventHandler(OnMessage);
            irc.OnQueryMessage += new IrcEventHandler(OnMessage);
            Connect(server, port);
        }


        void LoadPlugins()
        {
            // Load plugins and announce we're doing so.
            log.Message("Loading plugins...");
            plugins.LoadPlugins(ircComm, meidoComm);
            // Print number and descriptions of loaded plugins.
            log.Message("Done! Loaded {0} plugin(s):", plugins.Count);
            foreach (string s in plugins.GetDescriptions())
                log.Message("- " + s);
        }
        

        void Connect(string server, int port)
        {
            log.Message("Trying to connect to {0}:{1} ...", server, port);
            try
            {
                irc.Connect(server, port);
            }
            catch (CouldNotConnectException ex)
            {
                log.Error("Could not connect.", ex);
            }
        }


        // ---------------
        // Event handlers.
        // ---------------


        // Tell the server who we are and join channel(s).
        void OnConnected(object sender, EventArgs e)
        {
            irc.Login(nick, "Meido Bot", 0, "MeidoBot");

            log.Message("Success! Connected as {0} to {1}", irc.Nickname, irc.Address);

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


        // -------------------------
        // Special trigger handling.
        // -------------------------


        void SpecialTriggers(IIrcMessage msg)
        {
            if (msg.Trigger == "h" || msg.Trigger == "help")
                msg.Reply( Help(msg.MessageArray) );

            else if (msg.Trigger == "auth")
                msg.Reply( Auth(msg.Nick, msg.MessageArray) );

            else if (msg.Trigger == "disconnect" && meidoComm.AuthLevel(msg.Nick) == 10)
            {
                // Disconnect from IRC before stopping the plugins, thereby ensuring that once the order to stop has
                // been given no new messages will arrive.
                irc.Disconnect();
                plugins.StopPlugins();
            }
            
            else if (msg.Trigger == "part" && msg.MessageArray.Length == 2 && meidoComm.AuthLevel(msg.Nick) == 10)
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