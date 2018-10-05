using System;
using System.IO;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class UrlTitler : IMeidoHook
{
    readonly IMeidoComm meido;
    readonly ILog log;

    readonly ChannelThreadManager manager;
    readonly QueryTriggers qTriggers;


    public string Name
    {
        get { return "URL Titling"; }
    }
    public string Version
    {
        get { return "1.15"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"disable", "disable - Temporarily disable URL-Titling for you in current channel."},
                {"enable", "enable - Re-enable (previously disabled) URL-Titling for you."},

                {"query", "query <url...> - Query given URL(s) and return title or error."},
                {"query-dbg", "query-dbg <url...> - Query given URL(s) and return title or error. " +
                    "(Includes extra information)"},
                
                {"dump", "dump <url...> - Dumps HTML content of given URL(s) to a local file for inspection. " +
                    "(Owner only)"}
            };
        }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


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
        irc.AddChannelMessageHandler(UrlHandler);
        irc.AddChannelActionHandler(UrlHandler);

        // Trigger handling.
        Triggers = new Trigger[] {
            new Trigger("disable", Disable, TriggerOption.ChannelOnly),
            new Trigger("enable", Enable, TriggerOption.ChannelOnly),

            new Trigger("dump", Dump),
            new Trigger("query", qTriggers.Query),
            new Trigger("q", qTriggers.Query),
            new Trigger("query-dbg", qTriggers.QueryDebug)
        };
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


    public void UrlHandler(IIrcMessage e)
    {
        // Only process messages that aren't a trigger call.
        if (e.Trigger == null)
            manager.EnqueueMessage(e.Channel, e.Nick, e.Message);
    }


    public void Disable(IIrcMessage e)
    {
        manager.DisableNick(e.Channel, e.Nick);
        e.SendNotice("Disabling URL-Titling for you. (In {0})", e.Channel);
    }

    public void Enable(IIrcMessage e)
    {
        if ( manager.EnableNick(e.Channel, e.Nick) )
            e.SendNotice("Re-enabling URL-Titling for you.");
    }


    public void Dump(IIrcMessage e)
    {
        // Only allow owner to dump (writing temp files).
        if (meido.AuthLevel(e.Nick) == 3)
            QueryTriggers.Dump(e);
    }
}