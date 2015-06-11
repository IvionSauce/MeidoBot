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
        readonly PluginManager plugins;
        readonly Logger log;

        // IRC Communication object to be passed along to the plugins, so they can respond freely through it.
        // Also used to call the relevant method(s) on receiving messages.
        readonly IrcComm ircComm;
        // MeidoBot Communication object, used for functions that concern both the bot-framework and the plugins
        // running in it.
        readonly MeidoComm meidoComm;

        readonly string nick;
        readonly List<string> channels;


        public Meido(MeidoConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            // We need these parameters for events, store them in fields.
            this.nick = config.Nickname;
            this.channels = config.Channels;

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
            // Register non-plugin triggers.
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

            // and connect to the server.
            Connect(config.ServerAddress, config.Port);
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

            log.Message("Connected as {0} to {1}", irc.Nickname, irc.Address);

            irc.RfcJoin(channels.ToArray());
            irc.Listen();
        }


        void OnInvited(object sender, InviteEventArgs e)
        {
            if (!channels.Contains(e.Channel))
            {
                channels.Add(e.Channel);
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
            meidoComm.RegisterTrigger("auth", Auth);
            meidoComm.RegisterTrigger("part", Part);
            meidoComm.RegisterTrigger("disconnect", Disconnect);
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

        // Auth trigger.
        void Auth(IIrcMessage msg)
        {
            if (msg.MessageArray.Length > 1)
            {
                if (meidoComm.Auth(msg.Nick, msg.MessageArray[1]))
                    msg.Reply("You've successfully authenticated.");
            }

            msg.Reply("Your current Authentication Level is " + meidoComm.AuthLevel(msg.Nick));
        }

        // Part trigger.
        void Part(IIrcMessage msg)
        {
            if (msg.MessageArray.Length == 2 && meidoComm.AuthLevel(msg.Nick) >= 2)
                irc.RfcPart(msg.MessageArray[1]);
        }

        // Disconnect trigger.
        void Disconnect(IIrcMessage msg)
        {
            if (meidoComm.AuthLevel(msg.Nick) == 3)
                Dispose();
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