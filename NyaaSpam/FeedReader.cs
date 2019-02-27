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
        // Assign it a value, else the C# compiler thinks it will be unassigned once the loop exits. Which is a
        // possibility, since feed.Items could be empty...
        DateTimeOffset latestPublish = DateTimeOffset.MinValue;
        // So let's track that (it's also nice for logging).
        int itemsSeen = 0;
        foreach (SyndicationItem item in feed.Items)
        {
            // Save the most recent publishing date for later.
            if (itemsSeen == 0)
                latestPublish = item.PublishDate;

            itemsSeen++;
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

        if (itemsSeen > 0)
        {
            log.Verbose("Read {0} item(s) from feed. Most recent publish date is {1:s}",
                        itemsSeen, latestPublish.ToLocalTime());
            
            lastPrintedTime = latestPublish;
            dtFile.Write(latestPublish);
        }
        else
            log.Error("No items were processed in ReadFeed: RSS/Atom feed had 0 items.");
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
        foreach (string cat in Categories(item))
        {
            if (skipCategories.Contains(cat))
                return true;
        }

        return false;
    }

    static IEnumerable<string> Categories(SyndicationItem item)
    {
        // Straightforward categories as per the standard (either RSS or Atom).
        foreach (SyndicationCategory cat in item.Categories)
            yield return cat.Name;

        /* Roundabout case for special snowflake (XML Namespace) categories.
         * I'm looking at you, nyaa.si, what's wrong with using the standard!?
         * 
         * Looking more closely at the standard (http://www.rssboard.org/rss-specification) for the 'category' element:
         * "The value of the element is a forward-slash-separated string that identifies a hierarchic location
         * in the indicated taxonomy."
         * Nyaa.si doesn't use a "forward-slash-seperated string" but instead the category is formatted as:
         * "Category - Sub-category" ... so technically it's CORRECT to not use the standard category tag. Damnit.
         * 
         * The relevant NyaaV2 git commit that changes this is cc957ccc96ab9f7d1c26485064f8e40068623998,
         * Github URL: https://github.com/nyaadevs/nyaa/pull/140
        */
        foreach (SyndicationElementExtension element in item.ElementExtensions)
        {
            if ("category".Equals(element.OuterName, StringComparison.OrdinalIgnoreCase))
            {
                yield return element.GetObject<string>();
            }
        }
    }
}