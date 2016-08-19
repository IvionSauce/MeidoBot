using System;
using System.Text;
using System.Collections.Generic;
using Meebey.SmartIrc4net;
// Using directives for plugin use.
using MeidoCommon;


namespace MeidoBot
{
    class Meido : IDisposable
    {
        volatile Ignores _ignores;
        public Ignores OtherBots
        {
            get { return _ignores; }
            set
            {
                if (value == null)
                    _ignores = new Ignores();
                else
                    _ignores = value;
            }
        }


        readonly IrcClient irc = new IrcClient();

        readonly LogWriter logWriter;
        readonly Logger log;

        // Plugin container (MEF) and manager.
        PluginManager plugins;
        // Provides 'auth' and 'admin' trigger.
        readonly Admin admin;
        // Provides help triggers 'help'/'h'.
        readonly Help help;

        // IRC Communication object to be passed along to the plugins, so they can respond freely through it.
        // Also used to call the relevant method(s) on receiving messages.
        readonly IrcComm ircComm;
        // MeidoBot Communication object, used for functions that concern both the bot-framework and the plugins
        // running in it.
        readonly MeidoComm meidoComm;

        // Configuration fields, used for initializing various helper classes and for events.
        readonly MeidoConfig conf;
        readonly List<string> currentChannels;


        public Meido(MeidoConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            // We need these parameters for events, store them.
            conf = config;
            currentChannels = new List<string>(config.Channels);

            // Initialize log factory for this server/instance.
            var logFac = new LogFactory(config.ServerAddress);
            // Set aside some logging for ourself.
            log = logFac.CreateLogger("Meido");

            var tManager = new ThrottleManager(log);
            logWriter = new LogWriter();
            var chatLog = SetupChatlog();

            ircComm = new IrcComm(irc, tManager, chatLog);
            meidoComm = new MeidoComm(config, tManager, logFac);

            help = new Help(config.TriggerPrefix);
            LoadPlugins();
            // Setup non-plugin triggers and register them.
            admin = new Admin(this, irc, meidoComm);
            RegisterSpecialTriggers();

            OtherBots = Ignores.FromFile(meidoComm.ConfPathTo("OtherBots"), log);

            // Setting some SmartIrc4Net properties and event handlers.
            SetProperties();
            SetHandlers();
        }


        IChatlogger SetupChatlog()
        {
            if (PathTools.CheckChatlogIO(conf.ChatlogDirectory, log))
            {
                return new Chatlogger(irc, logWriter, conf.ChatlogDirectory);
            }

            log.Error("No chatlogging due to failed IO checks.");
            return new DummyChatlogger();
        }

        void LoadPlugins()
        {
            plugins = new PluginManager();

            // Only load plugins if IO checks succeed.
            if (PathTools.CheckPluginIO(conf, log))
            {
                // Load plugins and announce we're doing so.
                log.Message("Loading plugins...");
                plugins.LoadPlugins(ircComm, meidoComm);
                help.RegisterHelp( plugins.GetHelpDicts() );
                // Print number and descriptions of loaded plugins.
                log.Message("Done! Loaded {0} plugin(s):", plugins.Count);
                foreach (string s in plugins.GetDescriptions())
                    log.Message("- " + s);
            }
            else
            {
                log.Error("Not loading plugins due to failed IO checks.");
                plugins.DummyInit();
            }
        }


        void SetProperties()
        {
            irc.CtcpVersion = "MeidoBot " + Program.Version;
            irc.ActiveChannelSyncing = true;
            irc.AutoJoinOnInvite = true;
            irc.AutoReconnect = true;
            //irc.AutoRejoin = true;
            irc.Encoding = Encoding.UTF8;
        }

        void SetHandlers()
        {
            irc.OnConnected += OnConnected;
            irc.OnInvite += OnInvited;

            irc.OnChannelMessage += OnMessage;
            irc.OnQueryMessage += OnMessage;

            irc.OnChannelAction += OnAction;
            irc.OnQueryAction += OnAction;

            irc.OnPart += OnPart;
            irc.OnKick += OnKick;

            // IrcClient should automatically respond to CTCP VERSION if CtcpVersion is set, but it does not.
            // So we gotta do it ourselves.
            irc.OnCtcpRequest += CtcpVersion;
        }


        // ---------------
        // Public methods.
        // ---------------


        public void Connect()
        {
            log.Message("Trying to connect to {0}:{1} ...", conf.ServerAddress, conf.ServerPort);
            try
            {
                irc.Connect(conf.ServerAddress, conf.ServerPort);
            }
            catch (CouldNotConnectException ex)
            {
                log.Error("Could not connect.", ex);
            }
        }

        public void Reconnect()
        {
            Disconnect();
            Connect();
        }

        public void Disconnect()
        {
            Disconnect(null);
        }

        public void Disconnect(string quitMsg)
        {
            if (string.IsNullOrWhiteSpace(quitMsg))
                irc.RfcQuit();
            else
                irc.RfcQuit(quitMsg);
            
            irc.Disconnect();
        }


        public void Dispose()
        {
            // Disconnect from IRC before stopping the plugins, thereby ensuring that once the order to stop has
            // been given no new messages will arrive.
            if (irc.IsConnected)
                Disconnect();

            plugins.StopPlugins();
            logWriter.Dispose();
        }

        // ---------------
        // Event handlers.
        // ---------------

        // Tell the server who we are and join channel(s).
        void OnConnected(object sender, EventArgs e)
        {
            irc.Login(conf.Nickname, "Meido Bot", 0, "MeidoBot");
            log.Message("Connected as {0} to {1}", irc.Nickname, irc.Address);

            // Join current channels, since those might differ from the configured channels due to invites.
            log.Message("Joining channels: " + string.Join(" ", currentChannels));
            irc.RfcJoin(currentChannels.ToArray());

            irc.Listen();
        }


        void OnInvited(object sender, InviteEventArgs e)
        {
            log.Message("Received invite from {0} for {1}", e.Who, e.Channel);

            // Keep track of channels we're invited to, so that we may rejoin after disconnects.
            if (!currentChannels.Contains(e.Channel))
            {
                currentChannels.Add(e.Channel);
                irc.SendMessage(SendType.Notice, e.Who,
                    "If you want your channel on the auto-join list, please contact the owner.");
            }
        }


        void OnPart(object sender, PartEventArgs e)
        {
            if (irc.IsMe(e.Who))
            {
                log.Message("Parting from {0}", e.Channel);
                currentChannels.Remove(e.Channel);
            }
        }

        void OnKick(object sender, KickEventArgs e)
        {
            if (irc.IsMe(e.Whom))
            {
                log.Message("Kicked from {0} by {1}", e.Channel, e.Who);
                currentChannels.Remove(e.Channel);
            }
        }


        void OnMessage(object sender, IrcEventArgs e)
        {
            DoHandlers(e, ircComm.ChannelMessageHandlers, ircComm.QueryMessageHandlers);
        }

        void OnAction(object sender, ActionEventArgs e)
        {
            DoHandlers(e, ircComm.ChannelActionHandlers, ircComm.QueryActionHandlers);
        }

        void DoHandlers(IrcEventArgs e, Action<IIrcMessage> channelHandler, Action<IIrcMessage> queryHandler)
        {
            var msg = new IrcMessage(ircComm, e.Data, conf.TriggerPrefix);

            if (!OtherBots.Contains(msg.Nick))
            {
                if (msg.Trigger != null)
                    meidoComm.FireTrigger(msg);

                if (channelHandler != null && msg.Channel != null)
                    channelHandler(msg);
                else if (queryHandler != null)
                    queryHandler(msg);
            }
        }


        void CtcpVersion(object s, CtcpEventArgs e)
        {
            if (e.CtcpCommand.Equals("version", StringComparison.OrdinalIgnoreCase))
            {
                irc.SendMessage(SendType.CtcpReply, e.Data.Nick, "VERSION " + irc.CtcpVersion);
            }
        }


        // -------------------------
        // Special trigger handling.
        // -------------------------

        void RegisterSpecialTriggers()
        {
            meidoComm.RegisterTrigger("h", help.Trigger);
            meidoComm.RegisterTrigger("help", help.Trigger);

            meidoComm.RegisterTrigger("auth", admin.AuthTrigger);
            meidoComm.RegisterTrigger("admin", admin.AdminTrigger);
        }

    }
}