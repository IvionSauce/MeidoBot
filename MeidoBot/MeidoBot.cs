using System;
using System.Text;
using System.Collections.Generic;
using Meebey.SmartIrc4net;


namespace MeidoBot
{
    class Meido : IDisposable
    {
        readonly IrcClient irc = new IrcClient();
        // Auto-reconnect handling.
        readonly AutoReconnect reconnect;

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
        // Dispatches messages and trigger calls.
        readonly Dispatcher dispatch;

        // Configuration fields, used for initializing various helper classes and for events.
        readonly MeidoConfig conf;
        readonly List<string> currentChannels;


        public Meido(MeidoConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // We need these parameters for events, store them.
            conf = config;
            currentChannels = new List<string>(config.Channels);

            // Initialize log factory for this server/instance.
            var logFac = new LogFactory(config.ServerAddress);
            // Set aside some logging for ourself.
            log = logFac.CreateLogger("Meido");

            // Throttling for triggers and outgoing messages.
            var tManager = new ThrottleManager(log);

            // Setup chatlogger and underlying LogWriter.
            logWriter = new LogWriter();
            var chatLog = SetupChatlog();

            ircComm = new IrcComm(irc, tManager, chatLog);
            meidoComm = new MeidoComm(config, logFac, log);

            var triggers = new Triggers(
                config.TriggerPrefix,
                tManager,
                logFac.CreateLogger("Triggers")
            );

            // This must be instantiated before loading plugins and their triggers.
            dispatch = new Dispatcher(
                ircComm,
                triggers,
                new IrcEventHandlers(log)
            );
            // Setup autoloading of ignores.
            meidoComm.LoadAndWatchConfig("Ignore", LoadIgnores);

            // Setup non-plugin triggers and register them.
            help = new Help(triggers);
            admin = new Admin(this, irc, meidoComm);
            RegisterSpecialTriggers(triggers);
            // Load plugins and setup their triggers/help.
            LoadPlugins(triggers);

            // Setting some SmartIrc4Net properties and event handlers.
            SetProperties();
            SetHandlers();
            reconnect = new AutoReconnect(irc);
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

        void LoadIgnores(string path)
        {
            dispatch.Ignore = Ignores.FromFile(path, log);
        }

        void LoadPlugins(Triggers triggers)
        {
            plugins = new PluginManager();
            plugins.PluginLoad += dispatch.ProcessPluginDeclares;
            plugins.PluginLoad += help.RegisterHelp;

            // Only load plugins if IO checks succeed.
            if (PathTools.CheckPluginIO(conf, log))
            {
                log.Message("Loading plugins...");
                plugins.LoadPlugins(ircComm, meidoComm);

                log.Message("Done! Loaded {0} plugin(s):", plugins.Count);
                foreach (string s in plugins.GetDescriptions())
                    log.Message("- " + s);
            }
            else
            {
                log.Error("Not loading plugins due to failed IO checks.");
                plugins = new PluginManager();
            }
        }


        void SetProperties()
        {
            irc.CtcpVersion = "MeidoBot " + Program.Version;
            irc.ActiveChannelSyncing = true;
            irc.AutoJoinOnInvite = true;
            irc.Encoding = Encoding.UTF8;
        }

        void SetHandlers()
        {
            irc.OnConnected += Connected;
            irc.OnInvite += Invited;

            irc.OnChannelMessage += dispatch.ChannelMessage;
            irc.OnQueryMessage += dispatch.QueryMessage;

            irc.OnChannelAction += dispatch.ChannelAction;
            irc.OnQueryAction += dispatch.QueryAction;

            irc.OnPart += Part;
            irc.OnKick += Kick;

            irc.OnCtcpRequest += Ctcp;
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

            reconnect.Enabled = false;
            irc.Disconnect();
        }


        public void Dispose()
        {
            // Disconnect from IRC before stopping the plugins, thereby ensuring that once the order to stop has
            // been given no new messages will arrive.
            if (irc.IsConnected)
                Disconnect();

            dispatch.Dispose();
            plugins.Dispose();
            logWriter.Dispose();
        }

        // ---------------
        // Event handlers.
        // ---------------

        void Connected(object sender, EventArgs e)
        {
            irc.Login(conf.Nickname, "Meido Bot", 0, "MeidoBot");
            log.Message("Connected as {0} to {1}", irc.Nickname, irc.Address);

            // Join current channels, since those might differ from the configured channels due to invites.
            log.Message("Joining channels: " + string.Join(" ", currentChannels));
            irc.RfcJoin(currentChannels.ToArray());

            // Because we call `Listen` in this event handler no other OnConnected handlers will be called,
            // since `Listen` is a blocking call. So we got to inform `AutoReconnect` manually.
            reconnect.SuccessfulConnect();
            irc.Listen();
        }


        void Invited(object sender, InviteEventArgs e)
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


        void Part(object sender, PartEventArgs e)
        {
            if (irc.IsMe(e.Who))
            {
                log.Message("Parting from {0}", e.Channel);
                currentChannels.Remove(e.Channel);
            }
        }

        void Kick(object sender, KickEventArgs e)
        {
            if (irc.IsMe(e.Whom))
            {
                log.Message("Kicked from {0} by {1}", e.Channel, e.Who);
                currentChannels.Remove(e.Channel);
            }
        }


        void Ctcp(object s, CtcpEventArgs e)
        {
            var ctcpCmd = e.CtcpCommand;
            if (!string.IsNullOrEmpty(e.CtcpParameter))
                ctcpCmd += e.CtcpParameter;

            log.Message("Received a CTCP {0} from {1}", ctcpCmd, e.Data.Nick);
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

        void RegisterSpecialTriggers(Triggers triggers)
        {
            triggers.SpecialTrigger("h", help.Trigger);
            triggers.SpecialTrigger("help", help.Trigger);

            triggers.SpecialTrigger("auth", admin.AuthTrigger);
            triggers.SpecialTrigger("admin", admin.AdminTrigger);
        }

    }
}