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
        readonly IrcClient irc = new IrcClient();

        readonly Admin admin;
        readonly PluginManager plugins;
        readonly Logger log;

        // IRC Communication object to be passed along to the plugins, so they can respond freely through it.
        // Also used to call the relevant method(s) on receiving messages.
        readonly IrcComm ircComm;
        // MeidoBot Communication object, used for functions that concern both the bot-framework and the plugins
        // running in it.
        readonly MeidoComm meidoComm;

        readonly MeidoConfig conf;
        readonly List<string> currentChannels;


        public Meido(MeidoConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            // We need these parameters for events, store them.
            conf = config;
            currentChannels = new List<string>(conf.Channels);

            // Initialize the IrcComm with the IrcClient for this server/instance.
            ircComm = new IrcComm(irc);

            // Initialize the MeidoComm with the log factory for this server/instance.
            var logFac = new LogFactory(config.ServerAddress);
            meidoComm = new MeidoComm(logFac);
            // Set aside some logging for ourself.
            log = logFac.CreateLogger("MEIDO");

            // Setup plugins and load them.
            plugins = new PluginManager(config.TriggerPrefix);
            LoadPlugins();

            // Setup non-plugin triggers and register them.
            admin = new Admin(irc, meidoComm);
            RegisterSpecialTriggers();

            // Setting some SmartIrc4Net options/properties.
            SetProperties();

            // Set event handlers...
            irc.OnConnected += new EventHandler(OnConnected);
            irc.OnInvite += new InviteEventHandler(OnInvited);

            irc.OnChannelMessage += new IrcEventHandler(OnMessage);
            irc.OnQueryMessage += new IrcEventHandler(OnMessage);

            irc.OnChannelAction += new ActionEventHandler(ChannelAction);
            irc.OnQueryAction += new ActionEventHandler(QueryAction);
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


        void SetProperties()
        {
            irc.CtcpVersion = "MeidoBot " + Program.Version;
            irc.ActiveChannelSyncing = true;
            irc.AutoJoinOnInvite = true;
            irc.AutoReconnect = true;
            irc.AutoRejoin = true;
            irc.Encoding = Encoding.UTF8;
        }


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


        void OnMessage(object sender, IrcEventArgs e)
        {
            var msg = new IrcMessage(e.Data, plugins.Prefix);
            
            if (msg.Trigger != null)
            {
                if (ircComm.TriggerHandlers != null)
                    ircComm.TriggerHandlers(msg);

                meidoComm.FireTrigger(msg);
            }

            if (msg.Channel != null)
                ChannelMessage(msg);
            else
                QueryMessage(msg);
        }


        void ChannelAction(object sender, ActionEventArgs e)
        {
            if (ircComm.ChannelActionHandlers != null)
                ircComm.ChannelActionHandlers( new IrcMessage(e.Data, plugins.Prefix) );
        }

        void QueryAction(object sender, ActionEventArgs e)
        {
            if (ircComm.QueryActionHandlers != null)
                ircComm.QueryActionHandlers( new IrcMessage(e.Data, plugins.Prefix) );
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

        void RegisterSpecialTriggers()
        {
            meidoComm.RegisterTrigger("h", Help);
            meidoComm.RegisterTrigger("help", Help);

            meidoComm.RegisterTrigger("auth", admin.AuthTrigger);
            meidoComm.RegisterTrigger("admin", admin.AdminTrigger);
        }

        // Help trigger.
        void Help(IIrcMessage msg)
        {
            string subject = null;
            if (msg.MessageArray.Length > 1)
                subject = string.Join(" ", msg.MessageArray, 1, msg.MessageArray.Length - 1);

            if (string.IsNullOrWhiteSpace(subject))
            {
                string[] keys = plugins.GetHelpSubjects();
                var subjects = string.Join(", ", keys);

                msg.Reply("Help is available on - " + subjects);
            }
            else
            {
                string help = plugins.GetHelp(subject);

                if (help != null)
                    msg.Reply(help);
                else
                    msg.Reply("No help available.");
            }
        }


        public void Dispose()
        {
            // Disconnect from IRC before stopping the plugins, thereby ensuring that once the order to stop has
            // been given no new messages will arrive.
            irc.Disconnect();
            plugins.StopPlugins();
        }
    }
}