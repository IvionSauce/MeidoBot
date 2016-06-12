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

    readonly ChannelThreadManager manager;
    readonly QueryTriggers qTriggers;


    public string Name
    {
        get { return "URL Titling"; }
    }
    public string Version
    {
        get { return "1.14"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"disable", "disable - Temporarily disable URL-Titling for you in current channel."},
                {"enable", "enable - Re-enable (previously disabled) URL-Titling for you."},

                {"reload_bw", "reload_bw - Reload black- and whitelist from disk. (Owner only)"},

                {"query", "query <url...> - Query given URL(s) and return title or error."},
                {"query-dbg", "query-dbg <url...> - Query given URL(s) and return title or error. " +
                    "(Includes extra information)"},
                
                {"dump", "dump <url...> - Dumps HTML content of given URL(s) to a local file for inspection. " +
                    "(Owner only)"}
            };
        }
    }


    public void Stop()
    {
        manager.StopAll();
    }

    [ImportingConstructor]
    public UrlTitler(IIrcComm irc, IMeidoComm meido)
    {
        var log = meido.CreateLogger(this);
        var conf = new Config(meido.ConfPathTo("UrlTitling.xml"), log);

        // Sharing stuff with all the ChannelThreads.
        manager = new ChannelThreadManager(irc, log, conf);
        SetupBWLists(conf, log);

        qTriggers = new QueryTriggers(conf);

        // For handling messages/actions that can potentially containg URL(s).
        irc.AddChannelMessageHandler(UrlHandler);
        irc.AddChannelActionHandler(UrlHandler);

        // Trigger handling.
        meido.RegisterTrigger("disable", Disable, true);
        meido.RegisterTrigger("enable", Enable, true);
        meido.RegisterTrigger("reload_bw", ReloadBW);

        meido.RegisterTrigger("dump", Dump);
        meido.RegisterTrigger("query", qTriggers.Query);
        meido.RegisterTrigger("q", qTriggers.Query);
        meido.RegisterTrigger("query-dbg", qTriggers.QueryDebug);

        this.meido = meido;
    }

    void SetupBWLists(Config conf, ILog log)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(conf.BlacklistLocation))
            {
                manager.Blacklist.LoadFromFile(conf.BlacklistLocation);
                log.Message("-> Loaded blacklist from " + conf.BlacklistLocation);
            }
            if (!string.IsNullOrWhiteSpace(conf.WhitelistLocation))
            {
                manager.Whitelist.LoadFromFile(conf.WhitelistLocation);
                log.Message("-> Loaded whitelist from " + conf.WhitelistLocation);
            }
        }
        catch (IOException)
        {
            // Ignore IO errors.
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


    public void ReloadBW(IIrcMessage e)
    {
        if (meido.AuthLevel(e.Nick) == 3)
        {
            manager.Blacklist.ReloadFile();
            manager.Whitelist.ReloadFile();
            e.Reply("Black- and whitelist have been reloaded.");
        }
    }


    public void Dump(IIrcMessage e)
    {
        // Only allow owner to dump (writing temp files).
        if (meido.AuthLevel(e.Nick) == 3)
            QueryTriggers.Dump(e);
    }
}