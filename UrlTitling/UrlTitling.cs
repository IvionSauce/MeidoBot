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
        get { return "0.988"; }
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


    [ImportingConstructor]
    public UrlTitler(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        irc = ircComm;
        var conf = new Config(meidoComm.ConfDir + "/UrlTitling.xml");

        WebToIrc.Threshold = conf.Threshold;
        WebToIrc.Cookies.Add(conf.CookieColl);

        ChannelThreadManager.Blacklist = new ControlList();
        try
        {
            ChannelThreadManager.Blacklist.LoadFromFile(conf.BlacklistLocation);
            Console.WriteLine("-> Loaded blacklist from " + conf.BlacklistLocation);
        }
        // Purposefuly catch these exceptions, a blacklist is optional and it's to the user to make sure it's there
        // where they say it is. :V
        catch (FileNotFoundException)
        {}
        catch (DirectoryNotFoundException)
        {}

        // Sharing stuff with the Channel Manager.
        ChannelThreadManager.irc = ircComm;
        ChannelThreadManager.Conf = conf;

        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        // ----- Trigger handling -----
        switch (e.MessageArray[0])
        {
        case ".reload_bl":
            ChannelThreadManager.Blacklist.ReloadFile();
            irc.SendMessage(e.Channel, "Blacklist has been reloaded.");
            return;
        case ".disable":
            nickDisable.Add(e.Nick, e.Channel);
            irc.SendNotice(e.Nick, string.Format("Disabling URL-Titling for you. (In {0})", e.Channel));
            return;
        case ".enable":
            if (nickDisable.Remove(e.Nick, e.Channel))
                irc.SendNotice(e.Nick, "Re-enabling URL-Titling for you.");
            return;
        // Do nothing if it might be a trigger we're not associated with.
        case ".":
            return;
        }

        // ----- Handling of URLs -----
        bool printedDetection = false;
        foreach (string s in e.MessageArray)
        {
            if (s.StartsWith("http://") || s.StartsWith("https://"))
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
    static public ControlList Blacklist { get; set; }
    static public Config Conf { get; set; }

    static Dictionary<string, ChannelThread> channelThreads = new Dictionary<string, ChannelThread>();


    static public void EnqueueUrl(string channel, string nick, string url)
    {
        ChannelThread thread = GetThread(channel);

        lock (thread._channelLock)
        {
            thread.UrlQueue.Enqueue( new string[] {nick, url} );
            Monitor.Pulse(thread._channelLock);
        }
    }

    static ChannelThread GetThread(string channel)
    {
        string chanLow = channel.ToLower();

        ChannelThread thread;
        if (channelThreads.TryGetValue(chanLow, out thread))
            return thread;
        else
        {
            channelThreads.Add(chanLow, new ChannelThread(channel));
            return channelThreads[chanLow];
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

            if (Blacklist.IsInList(url, Channel, nick))
            {
                Console.WriteLine("Blacklisted: " + url);
                return;
            }

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
                Consumer(item);
            }
        }
    }
}