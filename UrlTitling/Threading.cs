using System;
using System.Threading;
using System.Collections.Generic;
using MeidoCommon;
using WebIrc;
using IvionSoft;


class ChannelThreadManager
{
    public readonly Blacklist Blacklist;
    public readonly Whitelist Whitelist;
    
    readonly IIrcComm irc;
    readonly ILog log;
    Config conf;

    readonly Dictionary<string, ChannelThread> channelThreads =
        new Dictionary<string, ChannelThread>(StringComparer.OrdinalIgnoreCase);
    
    
    public ChannelThreadManager(IIrcComm irc, ILog log)
    {
        this.irc = irc;
        this.log = log;
        
        Blacklist = new Blacklist();
        Whitelist = new Whitelist();
    }


    public void Configure(Config conf)
    {
        this.conf = conf;
        lock (channelThreads)
        {
            foreach (ChannelThread thread in channelThreads.Values)
            {
                thread.WebToIrc = conf.ConstructWebToIrc(thread.Channel);
            }
        }
    }
    
    
    public void EnqueueMessage(string channel, string nick, string message)
    {
        var item = new MessageItem(nick, message);
        ChannelThread thread = GetThread(channel);
        
        lock (thread.ChannelLock)
        {
            thread.MessageQueue.Enqueue(item);
            Monitor.Pulse(thread.ChannelLock);
        }
    }


    public void StopAll()
    {
        lock (channelThreads)
        {
            foreach (ChannelThread thread in channelThreads.Values)
            {
                lock (thread.ChannelLock)
                {
                    thread.MessageQueue.Enqueue(null);
                    Monitor.Pulse(thread.ChannelLock);
                }
            }
        }
    }
    
    
    public bool DisableNick(string channel, string nick)
    {
        ChannelThread thread = GetThread(channel);

        // Hijack the channelLock to serialize modifications (Add/Remove) to DisabledNicks.
        lock (thread.ChannelLock)
        {
            return thread.DisabledNicks.Add(nick);
        }
    }
    
    public bool EnableNick(string channel, string nick)
    {
        ChannelThread thread = GetThread(channel);
        
        lock (thread.ChannelLock)
        {
            return thread.DisabledNicks.Remove(nick);
        }
    }
    
    
    ChannelThread GetThread(string channel)
    {
        ChannelThread thread;

        lock (channelThreads)
        {
            if (!channelThreads.TryGetValue(channel, out thread))
            {
                var wIrc = conf.ConstructWebToIrc(channel);
                thread = new ChannelThread(irc, log, Blacklist, Whitelist, wIrc, channel);
                channelThreads[channel] = thread;
            }

            return thread;
        }
    }
}


class ChannelThread
{
    public readonly string Channel;
    public volatile WebToIrc WebToIrc;

    public readonly object ChannelLock;
    public readonly Queue<MessageItem> MessageQueue;
    public readonly HashSet<string> DisabledNicks;


    readonly IIrcComm irc;
    readonly ILog log;

    readonly Blacklist blacklist;
    readonly Whitelist whitelist;
    readonly ShortHistory<string> urlHistory = new ShortHistory<string>(3);
    
    
    public ChannelThread(IIrcComm irc,
                         ILog log,
                         Blacklist black,
                         Whitelist white,
                         WebToIrc wIrc,
                         string channel)
    {
        this.irc = irc;
        this.log = log;

        blacklist = black;
        whitelist = white;

        WebToIrc = wIrc;
        
        Channel = channel;
        MessageQueue = new Queue<MessageItem>();
        DisabledNicks = new HashSet<string>();
        ChannelLock = new object();
        
        new Thread(Consume).Start();
    }
    
    
    void Consume()
    {
        Thread.CurrentThread.Name = "URLs " + Channel;

        MessageItem item;
        while (true)
        {
            lock (ChannelLock)
            {
                while (MessageQueue.Count == 0)
                    Monitor.Wait(ChannelLock);
                
                item = MessageQueue.Dequeue();
            }
            
            if (item != null)
                ProcessMessage(item);
            // A queued null is the signal to stop, so return and stop consuming.
            else
                return;
        }
    }


    void ProcessMessage(MessageItem item)
    {
        string[] urls = UrlTools.Extract(item.Message);
        if (urls.Length > 0)
        {
            log.Verbose("{0}/{1} {2}", Channel, item.Nick, item.Message);

            if (!DisabledNicks.Contains(item.Nick))
            {
                foreach (string url in urls)
                    ProcessUrl(item.Nick, url);
            }
            else
                log.Message("Titling disabled for {0}.", item.Nick);
        }
    }
    
    
    void ProcessUrl(string nick, string url)
    {
        // If we haven't seen the URL recently.
        if (urlHistory.Add(url))
        {
            bool? inWhite = whitelist.IsInList(url, Channel, nick);
            // Check blacklist if whitelist isn't applicable.
            if (inWhite == null)
            {
                if ( blacklist.IsInList(url, Channel, nick) )
                    log.Message("Blacklisted: {0}", url);
                else
                    OutputUrl(url);
            }

            // If in whitelist, go directly to output and skip blacklist.
            else if (inWhite == true)
                OutputUrl(url);
            // If the whitelist was applicable, but the URL wasn't found in it.
            else
                log.Message("Not whitelisted: {0}", url);
        }
        // If we have seen the URL recently, don't output it.
        else
            log.Message("Spam suppression: {0}", url);
    }

    
    void OutputUrl(string url)
    {
        var result = WebToIrc.WebInfo(url);

        if (result.Success)
        {
            if (result.PrintTitle)
                irc.SendMessage(Channel, UrlTools.Filter(result.Title));

            log.Message(result.Messages);
            log.Message("{0} -- {1}", result.Requested, result.Title);
        }
        else
        {
            log.Message( ReportError(result) );
        }
    }

    string ReportError(TitlingResult result)
    {
        const string errorMsg = "Error getting {0} ({1})";
        if (result.Retrieved == null)
            return string.Format(errorMsg, result.Requested, result.Exception.Message);
        else
            return string.Format(errorMsg, result.Retrieved, result.Exception.Message);
    }
}


class MessageItem
{
    public string Nick { get; private set; }
    public string Message { get; private set; }
    
    
    public MessageItem(string nick, string message)
    {
        Nick = nick;
        Message = message;
    }
}