using System;
using System.IO;
using System.Collections.Generic;
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
        get { return "URL Titling Service"; }
    }
    public string Version
    {
        get { return "1.0 RC6"; }
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
        var conf = new Config(meidoComm.ConfDir + "/UrlTitling.xml");

        WebToIrc.Cookies.Add(conf.CookieColl);

        // Sharing stuff with all the ChannelThreads.
        manager = new ChannelThreadManager(ircComm, conf);
        // Setup black- and whitelist.
        SetupBWLists(conf);

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    void SetupBWLists(Config conf)
    {
        if (!string.IsNullOrWhiteSpace(conf.BlacklistLocation))
        {
            try
            {
                manager.Blacklist.LoadFromFile(conf.BlacklistLocation);
                Console.WriteLine("-> Loaded blacklist from " + conf.BlacklistLocation);
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
                Console.WriteLine("-> Loaded whitelist from " + conf.WhitelistLocation);
            }
            catch (FileNotFoundException)
            {}
            catch (DirectoryNotFoundException)
            {}
        }
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        string index0 = e.MessageArray[0];
        // ----- Trigger handling -----
        if (index0 == Prefix + "reload_bw")
        {
            manager.Blacklist.ReloadFile();
            manager.Whitelist.ReloadFile();
            irc.SendMessage(e.Channel, "Black- and whitelist have been reloaded.");
        }
        else if (index0 == Prefix + "disable")
        {
            manager.DisableNick(e.Channel, e.Nick);
            irc.SendNotice(e.Nick, "Disabling URL-Titling for you. (In {0})", e.Channel);
        }
        else if (index0 == Prefix + "enable")
        {
            if ( manager.EnableNick(e.Channel, e.Nick ) )
                irc.SendNotice(e.Nick, "Re-enabling URL-Titling for you.");
        }
        // Do nothing if it might be a trigger we're not associated with.
        else if (e.Message.StartsWith(Prefix))
            return;

        // ----- Handling of URLs -----
        manager.EnqueueMessage(e.Channel, e.Nick, e.MessageArray);
    }
}