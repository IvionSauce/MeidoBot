using System;
using System.IO;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class UrlTitler : IMeidoHook, IPluginTriggers, IPluginIrcHandlers
{
    public string Name
    {
        get { return "URL Titling"; }
    }
    public string Version
    {
        get { return "1.16"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }
    public IEnumerable<IIrcHandler> IrcHandlers { get; private set; }


    readonly IMeidoComm meido;
    readonly ILog log;

    readonly ChannelThreadManager manager;
    readonly QueryTriggers qTriggers;


    public void Stop()
    {
        manager.StopAll();
    }

    [ImportingConstructor]
    public UrlTitler(IIrcComm irc, IMeidoComm meido)
    {
        this.meido = meido;
        log = meido.CreateLogger(this);

        manager = new ChannelThreadManager(irc, log);
        qTriggers = new QueryTriggers();

        // Setting up main configuration.
        var xmlConf = new XmlConfig2<Config>(
            Config.DefaultConfig(),
            (xml) => new Config(xml),
            log,
            manager.Configure, qTriggers.Configure
        );
        meido.LoadAndWatchConfig("UrlTitling.xml", xmlConf);

        // Setting up black- and whitelist configuration.
        meido.LoadAndWatchConfig("blacklist", WrappedIO(LoadBlacklist));
        meido.LoadAndWatchConfig("whitelist", WrappedIO(LoadWhitelist));

        // For handling messages/actions that can potentially containg URL(s).
        IrcHandlers = new IIrcHandler[] {
            new IrcHandler<IIrcMsg>(UrlHandler)
        };

        // Trigger handling.
        Triggers = Trigger.Group(
            
            new Trigger("disable", Disable, TriggerOption.ChannelOnly) {
                Help = new TriggerHelp(
                    "Temporarily disable URL-Titling for you in current channel.")
            },
            new Trigger("enable", Enable, TriggerOption.ChannelOnly) {
                Help = new TriggerHelp(
                    "Re-enable (previously disabled) URL-Titling for you.")
            }
        ).AddGroup(
            new Trigger(msg => qTriggers.Query(msg, false), TriggerThreading.Threadpool,
                        "query", "q") {
                Help = new TriggerHelp(
                    "<url...>",
                    "Query given URL(s) and return title or error.")
            },
            new Trigger(msg => qTriggers.Query(msg, true), TriggerThreading.Threadpool,
                        "query-dbg", "qd") {
                Help = new TriggerHelp(
                    "<url...>",
                    "Query given URL(s) and return title or error. (Includes extra information)")
            }
        ).AddGroup(
            new Trigger("dump", Dump) {
                Help = new TriggerHelp(
                    "<url...>",
                    "Dumps HTML content of given URL(s) to a local file for inspection. (Owner only)")
            }
        );
    }


    static Action<string> WrappedIO(Action<string> loader)
    {
        Action<string> wrappedFunc = (path) =>
        {
            try
            {
                loader(path);
            }
            catch (IOException)
            {
                // Ignore IO errors.
            }
        };

        return wrappedFunc;
    }

    void LoadBlacklist(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            manager.Blacklist.LoadFromFile(path);
            log.Message("-> Loaded blacklist from " + path);
        }
    }
    void LoadWhitelist(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            manager.Whitelist.LoadFromFile(path);
            log.Message("-> Loaded whitelist from " + path);
        }
    }


    public void UrlHandler(IIrcMsg e)
    {
        // Only process messages that aren't a trigger call.
        if (e.Trigger == null)
            manager.EnqueueMessage(e.Channel, e.Nick, e.Message);
    }


    public void Disable(ITriggerMsg e)
    {
        manager.DisableNick(e.Channel, e.Nick);
        e.SendNotice("Disabling URL-Titling for you. (In {0})", e.Channel);
    }

    public void Enable(ITriggerMsg e)
    {
        if ( manager.EnableNick(e.Channel, e.Nick) )
            e.SendNotice("Re-enabling URL-Titling for you.");
    }


    public void Dump(ITriggerMsg e)
    {
        // Only allow owner to dump (writing temp files).
        if (meido.AuthLevel(e.Nick) == 3)
            QueryTriggers.Dump(e);
    }
}