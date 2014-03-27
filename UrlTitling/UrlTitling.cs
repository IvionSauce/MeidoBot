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

    public string Prefix { get; set; }

    public string Name
    {
        get { return "URL Titling Service"; }
    }
    public string Version
    {
        get { return "1.0 RC3"; }
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
        var conf = new Config(meidoComm.ConfDir + "/UrlTitling.xml");

        WebToIrc.Threshold = conf.Threshold;
        WebToIrc.Cookies.Add(conf.CookieColl);
        // Setup black- and whitelist.
        SetupBWLists(conf);
        // Sharing stuff with all the ChannelThreads.
        ChannelThread.irc = ircComm;
        ChannelThread.Conf = conf;

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    void SetupBWLists(Config conf)
    {
        ChannelThread.Blacklist = new Blacklist();
        ChannelThread.Whitelist = new Whitelist();
        if (!string.IsNullOrWhiteSpace(conf.BlacklistLocation))
        {
            try
            {
                ChannelThread.Blacklist.LoadFromFile(conf.BlacklistLocation);
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
                ChannelThread.Whitelist.LoadFromFile(conf.WhitelistLocation);
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
            ChannelThread.Blacklist.ReloadFile();
            ChannelThread.Whitelist.ReloadFile();
            irc.SendMessage(e.Channel, "Black- and whitelist have been reloaded.");
        }
        else if (index0 == Prefix + "disable")
        {
            ChannelThreadManager.DisableNick(e.Channel, e.Nick);
            irc.SendNotice(e.Nick, string.Format("Disabling URL-Titling for you. (In {0})", e.Channel));
        }
        else if (index0 == Prefix + "enable")
        {
            if ( ChannelThreadManager.EnableNick(e.Channel, e.Nick ) )
                irc.SendNotice(e.Nick, "Re-enabling URL-Titling for you.");
        }
        // Do nothing if it might be a trigger we're not associated with.
        else if (e.Message.StartsWith(Prefix))
            return;

        // ----- Handling of URLs -----
        ChannelThreadManager.EnqueueMessage(e.Channel, e.Nick, e.MessageArray);
    }
}


static class ChannelThreadManager
{
    static Dictionary<string, ChannelThread> channelThreads =
        new Dictionary<string, ChannelThread>(StringComparer.OrdinalIgnoreCase);


    static public void EnqueueMessage(string channel, string nick, string[] message)
    {
        var item = new MessageItem(nick, message);
        ChannelThread thread = GetThread(channel);

        lock (thread._channelLock)
        {
            thread.MessageQueue.Enqueue(item);
            Monitor.Pulse(thread._channelLock);
        }
    }


    static public void StopAll()
    {
        foreach (ChannelThread thread in channelThreads.Values)
        {
            lock (thread._channelLock)
            {
                thread.MessageQueue.Enqueue(null);
                Monitor.Pulse(thread._channelLock);
            }
        }
    }


    static public bool DisableNick(string channel, string nick)
    {
        ChannelThread thread = GetThread(channel);

        lock (thread._channelLock)
        {
            return thread.DisabledNicks.Add(nick);
        }
    }

    static public bool EnableNick(string channel, string nick)
    {
        ChannelThread thread = GetThread(channel);

        lock (thread._channelLock)
        {
            return thread.DisabledNicks.Remove(nick);
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
}


class ChannelThread
{
    // Shared amongst all threads/channels.
    // Are set from outside.
    public static IIrcComm irc { get; set; }
    public static Blacklist Blacklist { get; set; }
    public static Whitelist Whitelist { get; set; }
    public static Config Conf { get; set; }

    // Unique to each thread, channel-specific properties.
    public string Channel { get; private set; }
    public Queue<MessageItem> MessageQueue { get; private set; }
    public HashSet<string> DisabledNicks { get; private set; }
    
    public object _channelLock { get; private set; }
    
    WebToIrc webToIrc;
    
    
    public ChannelThread(string channel)
    {
        Channel = channel;
        MessageQueue = new Queue<MessageItem>();
        DisabledNicks = new HashSet<string>();
        _channelLock = new object();
        
        webToIrc = Conf.ConstructWebToIrc(channel);
        
        new Thread(Consume).Start();
    }


    void Consume()
    {
        MessageItem item;
        while (true)
        {
            lock (_channelLock)
            {
                while (MessageQueue.Count == 0)
                    Monitor.Wait(_channelLock);
                
                item = MessageQueue.Dequeue();
            }
            
            if (item != null)
            {
                bool printedDetection = false;
                foreach ( string url in ExtractUrls(item.Message) )
                {
                    if (!printedDetection)
                    {
                        var message = string.Join(" ", item.Message);
                        Console.WriteLine("\nURL(s) detected - {0}/{1} {2}", Channel, item.Nick, message);
                        printedDetection = true;
                    }

                    ProcessUrl(item.Nick, url);
                }
            }
            // A queued null is the signal to stop, so return and stop consuming.
            else
                return;
        }
    }


    IEnumerable<string> ExtractUrls(string[] message)
    {
        foreach (string s in message)
        {
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {                
                yield return s;
            }
        }
    }


    void ProcessUrl(string nick, string url)
    {        
        if (!DisabledNicks.Contains(nick))
        {
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
        else
            Console.WriteLine("Titling disabled for {0}.", nick);
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
}


class MessageItem
{
    public string Nick { get; private set; }
    public string[] Message { get; private set; }


    public MessageItem(string nick, string[] message)
    {
        Nick = nick;
        Message = message;
    }
}