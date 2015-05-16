using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using IvionWebSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class UrlTitler : IMeidoHook
{
    readonly ChannelThreadManager manager;


    public string Name
    {
        get { return "URL Titling"; }
    }
    public string Version
    {
        get { return "1.11"; }
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
    public UrlTitler(IIrcComm irc, IMeidoComm meido)
    {
        var log = meido.CreateLogger(this);
        var conf = new Config(Path.Combine(meido.ConfDir, "UrlTitling.xml"), log);

        //WebToIrc.Cookies.Add(conf.CookieColl);

        // Sharing stuff with all the ChannelThreads.
        manager = new ChannelThreadManager(irc, log, conf);
        SetupBWLists(conf, log);

        // For handling messages/actions that can potentially containg URL(s).
        irc.AddChannelMessageHandler(UrlHandler);
        irc.AddChannelActionHandler(UrlHandler);
        // Trigger handling.
        meido.RegisterTrigger("disable", Disable, true);
        meido.RegisterTrigger("enable", Enable, true);
        meido.RegisterTrigger("reload_bw", ReloadBW);
        meido.RegisterTrigger("dump", Dump);
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
        manager.Blacklist.ReloadFile();
        manager.Whitelist.ReloadFile();
        e.Reply("Black- and whitelist have been reloaded.");
    }

    public void Dump(IIrcMessage e)
    {
        if (e.MessageArray.Length > 1)
        {
            var encHelper = new HtmlEncodingHelper();
            var resource = encHelper.GetWebString(e.MessageArray[1]);
            if (resource.Success)
            {
                string path = Path.Combine(Path.GetTempPath(), "dump.html");
                File.WriteAllText(path, resource.Document);
            }
        }
    }

}