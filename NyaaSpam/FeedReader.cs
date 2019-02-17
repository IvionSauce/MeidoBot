using System;
using System.Xml;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel.Syndication;
using MeidoCommon;


class FeedReader
{
    readonly IIrcComm irc;
    readonly ILog log;

    readonly DateTimeFile dtFile;
    readonly Patterns patterns;

    volatile Timer tmr;
    DateTimeOffset lastPrintedTime;

    volatile Config conf;
    
    
    public FeedReader(IIrcComm irc, ILog log, DateTimeFile dtFile, Patterns patterns)
    {
        this.irc = irc;
        this.log = log;
        this.dtFile = dtFile;
        this.patterns = patterns;

        lastPrintedTime = dtFile.Read();
    }

    public void Configure(Config conf)
    {
        this.conf = conf;
    }

    
    public void Start()
    {
        var period = conf.Interval;

        tmr = new Timer(
            ReadFeed,
            null,
            DetermineDueTime(period),
            period
        );
    }

    public static TimeSpan DetermineDueTime(TimeSpan intervalTs)
    {
        const int hour = 60;
        var dueTime = intervalTs;

        int interval = intervalTs.Minutes;
        // If an hour is cleanly divided by specified interval, make the interval/period align on
        // multiples of the interval.
        if ((hour % interval) == 0)
        {
            var now = DateTimeOffset.Now;
            int mult = (now.Minute / interval) + 1;
            int mins = interval * mult;

            var wholeHour = new DateTimeOffset(
                now.Year, now.Month,
                now.Day, now.Hour,
                0, 0, now.Offset
            );
            var projected = wholeHour + TimeSpan.FromMinutes(mins);

            dueTime = projected - now;
        }

        return dueTime;
    }


    public void Stop()
    {
        tmr.Dispose();
    }


    void ReadFeed(object data)
    {
        var feed = OpenFeed();
        if (feed == null)
            return;

        SanitizeLastPrinted();
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
            if (Skip(item, conf.SkipCategories))
                continue;
            
            string[] channels = patterns.PatternMatch(item.Title.Text);
            foreach (string channel in channels)
            {
                irc.SendMessage(channel, "号外! 号外! 号外! \u0002:: {0} ::\u000F {1}", item.Title.Text, item.Id);
            }
        }
        lastPrintedTime = latestPublish;
        dtFile.Write(latestPublish);
    }

    SyndicationFeed OpenFeed()
    {
        SyndicationFeed feed = null;

        try
        {
            var feedUri = conf.Feed;

            XmlReader reader = XmlReader.Create(feedUri.OriginalString);
            feed = SyndicationFeed.Load(reader);
        }
        catch (System.Net.WebException ex)
        {
            log.Error("WebException in ReadFeed: " + ex.Message);
        }
        catch (XmlException ex)
        {
            log.Error("XmlException in ReadFeed: " + ex.Message);
        }

        return feed;
    }

    void SanitizeLastPrinted()
    {
        // Do not accept a DateTime too far in the past, to prevent flooding/spam.
        lastPrintedTime = InputTools.SanitizeDate(lastPrintedTime, TimeSpan.FromHours(24));
    }

    static bool Skip(SyndicationItem item, HashSet<string> skipCategories)
    {
        foreach (SyndicationCategory cat in item.Categories)
        {
            if (skipCategories.Contains(cat.Name))
                return true;
        }

        return false;
    }
}