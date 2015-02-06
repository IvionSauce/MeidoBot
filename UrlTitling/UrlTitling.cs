using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using WebIrc;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class UrlTitler : IMeidoHook
{
    readonly IIrcComm irc;
    readonly ChannelThreadManager manager;

    public string Prefix { get; set; }

    public string Name
    {
        get { return "URL Titling"; }
    }
    public string Version
    {
        get { return "1.1"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"disable", "disable - Temporarily disable URL-Titling for you in current channel."},
                {"enable", "enable - Re-enable (previously disabled) URL-Titling for you."}
            };
        }
    }


    public void Stop()
    {
        manager.StopAll();
    }

    [ImportingConstructor]
    public UrlTitler(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        var log = meidoComm.CreateLogger(this);
        var conf = new Config(Path.Combine(meidoComm.ConfDir, "UrlTitling.xml"), log);

        WebToIrc.Cookies.Add(conf.CookieColl);

        // Sharing stuff with all the ChannelThreads.
        manager = new ChannelThreadManager(ircComm, log, conf);
        SetupBWLists(conf, log);

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
        irc.AddTriggerHandler(HandleTrigger);
    }

    void SetupBWLists(Config conf, ILog log)
    {
        if (!string.IsNullOrWhiteSpace(conf.BlacklistLocation))
        {
            try
            {
                manager.Blacklist.LoadFromFile(conf.BlacklistLocation);
                log.Message("-> Loaded blacklist from " + conf.BlacklistLocation);
            }
            catch (FileNotFoundException)
            {}
            catch (DirectoryNotFoundException)
            {}
        }
        if (!string.IsNullOrWhiteSpace(conf.WhitelistLocation))
        {
            try
            {
                manager.Whitelist.LoadFromFile(conf.WhitelistLocation);
                log.Message("-> Loaded whitelist from " + conf.WhitelistLocation);
            }
            catch (FileNotFoundException)
            {}
            catch (DirectoryNotFoundException)
            {}
        }
    }


    public void HandleChannelMessage(IIrcMessage e)
    {
        switch (e.Trigger)
        {
        // Handling of URLs.
        case null:
            manager.EnqueueMessage(e.Channel, e.Nick, e.Message);
            return;
        // Trigger handling.
        case "disable":
            manager.DisableNick(e.Channel, e.Nick);
            irc.SendNotice(e.Nick, "Disabling URL-Titling for you. (In {0})", e.Channel);
            return;
        case "enable":
            if ( manager.EnableNick(e.Channel, e.Nick ) )
                irc.SendNotice(e.Nick, "Re-enabling URL-Titling for you.");
            return;
        }
    }


    public void HandleTrigger(IIrcMessage e)
    {
        switch (e.Trigger)
        {
        case "bin":
            ThreadPool.QueueUserWorkItem( (data) => BinaryPrint(e) );
            return;
        case "reload_bw":
            manager.Blacklist.ReloadFile();
            manager.Whitelist.ReloadFile();
            e.Reply("Black- and whitelist have been reloaded.");
            return;
        }
    }

    void BinaryPrint(IIrcMessage e)
    {
        for (int i = 1; i < e.MessageArray.Length; i++)
        {
            var msg = e.MessageArray[i];
            if (Uri.IsWellFormedUriString(msg, UriKind.Absolute))
            {
                var req = new TitlingRequest(new Uri(msg));
                var result = BinaryHandler.BinaryToIrc(req);
                
                if (result.PrintTitle)
                    e.Reply(result.Title);
                else
                    e.Reply(result.Exception.Message);
            }
        }
    }

}