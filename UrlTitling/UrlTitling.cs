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
    IIrcComm irc;

    NickDisable nickDisable = new NickDisable();

    public string Prefix { get; set; }

    public string Name
    {
        get { return "URL Titling Service"; }
    }
    public string Version
    {
        get { return "1.0 RC2"; }
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
        ChannelThreadManager.StopAll();
    }

    [ImportingConstructor]
    public UrlTitler(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        irc = ircComm;
        var conf = new Config(meidoComm.ConfDir + "/UrlTitling.xml");

        WebToIrc.Threshold = conf.Threshold;
        WebToIrc.Cookies.Add(conf.CookieColl);
        // Setup black- and whitelist.
        SetupBWLists(conf);
        // Sharing stuff with the Channel Manager.
        ChannelThreadManager.irc = ircComm;
        ChannelThreadManager.Conf = conf;

        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    void SetupBWLists(Config conf)
    {
        ChannelThreadManager.Blacklist = new Blacklist();
        ChannelThreadManager.Whitelist = new Whitelist();
        if (!string.IsNullOrEmpty(conf.BlacklistLocation))
        {
            try
            {
                ChannelThreadManager.Blacklist.LoadFromFile(conf.BlacklistLocation);
                Console.WriteLine("-> Loaded blacklist from " + conf.BlacklistLocation);
            }
            catch (FileNotFoundException)
            {}
            catch (DirectoryNotFoundException)
            {}
        }
        if (!string.IsNullOrEmpty(conf.WhitelistLocation))
        {
            try
            {
                ChannelThreadManager.Whitelist.LoadFromFile(conf.WhitelistLocation);
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
            ChannelThreadManager.Blacklist.ReloadFile();
            ChannelThreadManager.Whitelist.ReloadFile();
            irc.SendMessage(e.Channel, "Black- and whitelist have been reloaded.");
        }
        else if (index0 == Prefix + "disable")
        {
            nickDisable.Add(e.Nick, e.Channel);
            irc.SendNotice(e.Nick, string.Format("Disabling URL-Titling for you. (In {0})", e.Channel));
        }
        else if (index0 == Prefix + "enable")
        {
            if (nickDisable.Remove(e.Nick, e.Channel))
                irc.SendNotice(e.Nick, "Re-enabling URL-Titling for you.");
        }
        // Do nothing if it might be a trigger we're not associated with.
        else if (e.Message.StartsWith(Prefix))
            return;

        // ----- Handling of URLs -----
        bool printedDetection = false;
        foreach (string s in e.MessageArray)
        {
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!printedDetection)
                {
                    Console.WriteLine("\nURL(s) detected - {0}/{1} {2}", e.Channel, e.Nick, e.Message);
                    printedDetection = true;
                }

                if (nickDisable.IsNickDisabled(e.Nick, e.Channel))
                    Console.WriteLine("Titling disabled for {0}.", e.Nick);
                else
                    ChannelThreadManager.EnqueueUrl(e.Channel, e.Nick, s);
            }
        }
    }
}


static class ChannelThreadManager
{
    static public IIrcComm irc { get; set; }
    static public Blacklist Blacklist { get; set; }
    static public Whitelist Whitelist { get; set; }
    static public Config Conf { get; set; }

    static Dictionary<string, ChannelThread> channelThreads =
        new Dictionary<string, ChannelThread>(StringComparer.OrdinalIgnoreCase);


    static public void EnqueueUrl(string channel, string nick, string url)
    {
        ChannelThread thread = GetThread(channel);

        lock (thread._channelLock)
        {
            thread.UrlQueue.Enqueue( new string[] {nick, url} );
            Monitor.Pulse(thread._channelLock);
        }
    }

    static public void StopAll()
    {
        foreach (ChannelThread thread in channelThreads.Values)
        {
            lock (thread._channelLock)
            {
                thread.UrlQueue.Enqueue(null);
                Monitor.Pulse(thread._channelLock);
            }
        }
    }

    static ChannelThread GetThread(string channel)
    {
        ChannelThread thread;
        if (channelThreads.TryGetValue(channel, out thread))
            return thread;
        else
        {
            channelThreads.Add(channel, new ChannelThread(channel));
            return channelThreads[channel];
        }
    }


    class ChannelThread
    {
        public string Channel { get; private set; }
        public Queue<string[]> UrlQueue { get; private set; }

        public object _channelLock { get; private set; }

        WebToIrc webToIrc;


        public ChannelThread(string channel)
        {
            Channel = channel;
            UrlQueue = new Queue<string[]>();
            _channelLock = new object();

            webToIrc = Conf.ConstructWebToIrc(channel);

            new Thread(Consume).Start();
        }

        void Consumer(string[] item)
        {
            string nick = item[0];
            string url = item[1];

            bool? inWhite = Whitelist.IsInList(url, Channel, nick);
            // If whitelist not applicable.
            if (inWhite == null)
            {
                if ( Blacklist.IsInList(url, Channel, nick) )
                    Console.WriteLine("Blacklisted: " + url);
                else
                    OutputUrl(url);
            }
            // If in whitelist, go directly to output and skip blacklist.
            else if (inWhite == true)
                OutputUrl(url);
            // If the whitelist was applicable, but the URL wasn't found in it.
            else
                Console.WriteLine("Not whitelisted: " + url);
        }

        void OutputUrl(string url)
        {
            string htmlInfo = webToIrc.GetWebInfo(url);
            if (htmlInfo != null)
            {
                irc.SendMessage(Channel, htmlInfo);
                Console.WriteLine(url + "  --  " + htmlInfo);
            }
        }

        void Consume()
        {
            while (true)
            {
                string[] item;
                lock (_channelLock)
                {
                    while (UrlQueue.Count == 0)
                        Monitor.Wait(_channelLock);

                    item = UrlQueue.Dequeue();
                }
                if (item != null)
                    Consumer(item);
                else
                    return;
            }
        }
    }
}