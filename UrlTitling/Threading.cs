using System;
using System.Threading;
using System.Collections.Generic;
using MeidoCommon;
using WebIrc;


class ChannelThreadManager
{
    public Blacklist Blacklist { get; private set; }
    public Whitelist Whitelist { get; private set; }
    
    readonly IIrcComm irc;
    readonly ILog log;
    readonly Config conf;

    readonly Dictionary<string, ChannelThread> channelThreads =
        new Dictionary<string, ChannelThread>(StringComparer.OrdinalIgnoreCase);
    
    
    public ChannelThreadManager(IIrcComm irc, ILog log, Config conf)
    {
        this.irc = irc;
        this.log = log;
        this.conf = conf;
        
        Blacklist = new Blacklist();
        Whitelist = new Whitelist();
    }
    
    
    public void EnqueueMessage(string channel, string nick, string[] message)
    {
        var item = new MessageItem(nick, message);
        ChannelThread thread = GetThread(channel);
        
        lock (thread._channelLock)
        {
            thread.MessageQueue.Enqueue(item);
            Monitor.Pulse(thread._channelLock);
        }
    }
    
    
    public void StopAll()
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
    
    
    public bool DisableNick(string channel, string nick)
    {
        ChannelThread thread = GetThread(channel);

        // Hijack the channelLock to serialize modifications (Add/Remove) to DisabledNicks.
        lock (thread._channelLock)
        {
            return thread.DisabledNicks.Add(nick);
        }
    }
    
    public bool EnableNick(string channel, string nick)
    {
        ChannelThread thread = GetThread(channel);
        
        lock (thread._channelLock)
        {
            return thread.DisabledNicks.Remove(nick);
        }
    }
    
    
    ChannelThread GetThread(string channel)
    {
        ChannelThread thread;
        if (channelThreads.TryGetValue(channel, out thread))
            return thread;
        else
        {
            var wIrc = conf.ConstructWebToIrc(channel);
            thread = new ChannelThread(irc, log, Blacklist, Whitelist, wIrc, channel);
            
            channelThreads.Add(channel, thread);
            return thread;
        }
    }
}


class ChannelThread
{
    public readonly string Channel;

    public Queue<MessageItem> MessageQueue { get; private set; }
    public HashSet<string> DisabledNicks { get; private set; }
    
    public object _channelLock { get; private set; }
    
    readonly IIrcComm irc;
    readonly ILog log;
    readonly Blacklist blacklist;
    readonly Whitelist whitelist;
    
    readonly WebToIrc webToIrc;
    
    
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

        webToIrc = wIrc;
        
        Channel = channel;
        MessageQueue = new Queue<MessageItem>();
        DisabledNicks = new HashSet<string>();
        _channelLock = new object();
        
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
                ProcessMessage(item);
            // A queued null is the signal to stop, so return and stop consuming.
            else
                return;
        }
    }


    void ProcessMessage(MessageItem item)
    {
        if (!DisabledNicks.Contains(item.Nick))
        {
            foreach ( string url in ExtractUrls(item.Message) )         
                ProcessUrl(item.Nick, url);
        }
        else
            log.Message("Titling disabled for {0}.", item.Nick);
    }
    
    
    IEnumerable<string> ExtractUrls(string[] message)
    {
        foreach (string s in message)
        {
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                log.Verbose("URL detected in {0}: {1}", Channel, s);
                yield return s;
            }
        }
    }
    
    
    void ProcessUrl(string nick, string url)
    {
        bool? inWhite = whitelist.IsInList(url, Channel, nick);
        // If whitelist not applicable.
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
    
    
    void OutputUrl(string url)
    {
        var result = webToIrc.WebInfo(url);

        if (result.Success)
        {
            if (result.PrintTitle)
                irc.SendMessage(Channel, result.Title);

            log.Message("{0} -- {1}", result.Requested, result.Title);
            log.Message(result.Messages);
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
    public string[] Message { get; private set; }
    
    
    public MessageItem(string nick, string[] message)
    {
        Nick = nick;
        Message = message;
    }
}