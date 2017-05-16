using System;
using System.Xml;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel.Syndication;
using MeidoCommon;


class FeedReader
{
    public Uri Feed { get; private set; }
    public TimeSpan Interval { get; private set; }
    public Patterns Patterns { get; private set; }
    public HashSet<string> SkipCategories { get; private set; }

    Timer tmr;
    
    readonly IIrcComm irc;
    readonly ILog log;
    
    DateTimeOffset lastPrintedTime = DateTimeOffset.Now;
    
    
    public FeedReader(IIrcComm irc, ILog log, Config conf, Patterns patterns)
    {
        this.irc = irc;
        this.log = log;

        Feed = conf.Feed;
        Interval = TimeSpan.FromMinutes(conf.Interval);
        SkipCategories = conf.SkipCategories;
        Patterns = patterns;
    }
    
    public void Start()
    {
        tmr = new Timer(ReadFeed, null, Interval, Interval);
    }
    
    public void Stop()
    {
        tmr.Dispose();
    }
    
    void ReadFeed(object data)
    {        
        SyndicationFeed feed;
        try
        {
            XmlReader reader = XmlReader.Create(Feed.OriginalString);
            feed = SyndicationFeed.Load(reader);
        }
        catch (System.Net.WebException ex)
        {
            log.Error("WebException in ReadFeed: " + ex.Message);
            return;
        }
        catch (XmlException ex)
        {
            log.Error("XmlException in ReadFeed: " + ex.Message);
            return;
        }
        
        bool latestItem = true;
        // Assign it a value, else the C# compiler thinks it will be unassigned once the loop exits. But it _does_ get
        // assigned, in the first loop (but I can see why the compiler doesn't see this).
        DateTimeOffset latestPublish = DateTimeOffset.MinValue;
        foreach (SyndicationItem item in feed.Items)
        {
            // Since feed.Items is only IEnumerable, we can't just access the first member by index, so we have to
            // use this roundabout way. :/
            if (latestItem)
            {
                latestPublish = item.PublishDate;
                latestItem = false;
            }
            
            // Once we hit items that we probably already have printed, stop processing the rest.
            if (item.PublishDate <= lastPrintedTime)
                break;
            // Skip processing items in categories we don't care about.
            if (Skip(item))
                continue;
            
            string[] channels = Patterns.PatternMatch(item.Title.Text);
            foreach (string channel in channels)
            {
                irc.SendMessage(channel, "号外! 号外! 号外! \u0002:: {0} ::\u000F {1}", item.Title.Text, item.Id);
            }
        }
        lastPrintedTime = latestPublish;
    }

    bool Skip(SyndicationItem item)
    {
        foreach (SyndicationCategory cat in item.Categories)
        {
            if (SkipCategories.Contains(cat.Name))
                return true;
        }

        return false;
    }
}