using System;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.ServiceModel.Syndication;
using System.Collections.Generic;
using IvionSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class NyaaSpam : IMeidoHook
{
    IIrcComm irc;

    NyaaPatterns nyaa = new NyaaPatterns();
    NyaaFeedReader feedReader;

    public string Prefix { get; set; }

    public string Name
    {
        get { return "NyaaSpam"; }
    }
    public string Version
    {
        get { return "0.51"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"nyaa add", "add <pattern...> - Adds pattern(s), seperated by \",\". Unless enclosed in quotation " +
                    "marks (\"), in which case the pattern is added verbatim. (Ex: nyaa add show1, show2)"},
                {"nyaa del", "del <index...> - Removes pattern(s) inidicated by given indices. Can be seperated by " +
                    "\",\" and accepts ranges given as \"x-y\". (Ex: nyaa del 4, 7, 0-2)"},
                {"nyaa show", "show [index...] - Gives an overview of all patterns that are checked for. If given " +
                    "an index/indices it will show just those. (Ex: nyaa show) (Ex: nyaa show 4, 7, 0-2)"}
            };
        }
    }


    public void Stop()
    {
        feedReader.Stop();
    }

    [ImportingConstructor]
    public NyaaSpam(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        string nyaaFile = meidoComm.ConfDir + "/_nyaa";
        try
        {
            nyaa.LoadFromFile(nyaaFile);
        }
        catch (FileNotFoundException)
        {
            File.Create(nyaaFile).Dispose();
            nyaa.LoadFromFile(nyaaFile);
        }

        var conf = new Config(meidoComm.ConfDir + "/NyaaSpam.xml");
        feedReader = new NyaaFeedReader(ircComm, conf, nyaa);
        feedReader.Start();

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleMessage);
    }

    public void HandleMessage(IIrcMessage e)
    {
        if (e.MessageArray[0] == Prefix + "nyaa")
        {
            if (e.MessageArray.Length == 1)
                irc.SendMessage( e.Channel, string.Format("See \"{0}h nyaa <add|del|show>\" for help.", Prefix) );

            else if (e.MessageArray[1] == "add" && e.MessageArray.Length > 2)
            {
                Add( e.Channel, e.Nick, string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2) );
            }

            else if (e.MessageArray[1] == "del" && e.MessageArray.Length > 2)
            {
                Del( e.Channel, e.Nick, string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2) );
            }

            else if (e.MessageArray[1] == "show")
            {
                if (e.MessageArray.Length > 2)
                    Show( e.Channel, e.Nick, string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2) );
                else
                    Show(e.Channel, e.Nick);
            }

            /* else if (e.MessageArray[1] == "reload")
            {
                nyaa.ReloadFile();
                irc.SendMessage(e.Channel, "Patterns reloaded from disk.");
            }
            else if (e.MessageArray[1] == "save")
            {
                nyaa.WriteFile();
                irc.SendMessage(e.Channel, "Patterns saved to disk.");
            } */
        }
    }

    void Add(string channel, string nick, string patternsStr)
    {
        int amount = 0;
        // If enclosed in quotation marks, add string within the marks verbatim.
        if ( patternsStr.StartsWith("\"") && patternsStr.EndsWith("\"") )
        {
            // Slice off the quotation marks.
            string pattern = patternsStr.Substring(1, patternsStr.Length - 2);
            if (nyaa.Add(channel, pattern) != -1)
                amount++;
        }
        // Else interpret comma's as seperators between different titles.
        else
        {
            string[] patterns = patternsStr.Split(',');
            int index;
            foreach (string pattern in patterns)
            {
                if (pattern.StartsWith(" "))
                    index = nyaa.Add(channel, pattern.Substring(1));
                else
                    index = nyaa.Add(channel, pattern);

                if (index != -1)
                    amount++;
            }
        }
        irc.SendNotice( nick, string.Format("Added {0} pattern(s)", amount) );
    }

    void Del(string channel, string nick, string numbersStr)
    {
        int[] numbers = GetNumbers(numbersStr);
        Array.Reverse(numbers);

        string removedPattern = null;
        foreach (int i in numbers)
        {
            removedPattern = nyaa.Remove(channel, i);
            if (removedPattern != null)
                irc.SendNotice( nick, string.Format("Deleted: {0}", removedPattern) );
        }
        if (removedPattern != null)
            irc.SendNotice(nick, " -----");
    }

    void Show(string channel, string nick, string numbersStr)
    {
        int[] numbers = GetNumbers(numbersStr);

        string pattern = null;
        foreach (int i in numbers)
        {
            pattern = nyaa.Get(channel, i);
            if (pattern != null)
                irc.SendNotice( nick, string.Format("[{0}] \"{1}\"", i, pattern) );
        }
        if (pattern != null)
            irc.SendNotice(nick, " -----");
    }

    void Show(string channel, string nick)
    {
        irc.SendNotice( nick, string.Format("Patterns for {0}:", channel) );

        string[] patterns = nyaa.GetPatterns(channel);
        if (patterns != null)
        {
            int half = (int)Math.Ceiling(patterns.Length / 2d);

            int j;
            for (int i = 0; i < half; i++)
            {
                j = i + half;
                if (j < patterns.Length)
                    irc.SendNotice( nick, string.Format("[{0}] {1,-30} [{2}] {3}", i, patterns[i], j, patterns[j]) );
                else
                    irc.SendNotice( nick, string.Format("[{0}] {1}", i, patterns[i]) );
            }
        }

        irc.SendNotice(nick, " -----");
    }

    int[] GetNumbers(string numbersStr)
    {
        List<int> numbers = new List<int>();
        int num;
        foreach (string s in numbersStr.Split(','))
        {
            if (s.Contains("-"))
            {
                int[] range = GetRange(s);
                numbers.AddRange(range);
            }
            else if (int.TryParse(s, out num))
                numbers.Add(num);
        }

        numbers.Sort();
        return numbers.ToArray();
    }

    // Return array of ints for a range given as "x-y".
    int[] GetRange(string rangeStr)
    {
        string[] startEnd = rangeStr.Split('-');
        if (startEnd.Length != 2)
            return new int[] {};

        int start, end;
        var numbers = new List<int>();

        if ( int.TryParse(startEnd[0], out start) && int.TryParse(startEnd[1], out end) )
        {
            int num = start;
            while (num <= end)
            {
                numbers.Add(num);
                num++;
            }
        }
        return numbers.ToArray();
    }
}


class NyaaFeedReader
{
    public TimeSpan Interval { get; private set; }
    public HashSet<string> SkipCategories { get; private set; }
    public NyaaPatterns Nyaa { get; private set; }

    Timer tmr;

    IIrcComm irc;

    DateTimeOffset lastPrintedTime = DateTimeOffset.Now;
    DateTimeOffset latestPublish = DateTimeOffset.Now;


    public NyaaFeedReader(IIrcComm irc, Config conf, NyaaPatterns patterns)
    {
        this.irc = irc;

        Interval = TimeSpan.FromMinutes(conf.Interval);
        SkipCategories = conf.SkipCategories;
        Nyaa = patterns;
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
        if (Nyaa.ChangedSinceLastSave() == true)
            Nyaa.WriteFile();
        
        SyndicationFeed feed;
        try
        {
            XmlReader reader = XmlReader.Create("http://www.nyaa.se/?page=rss");
            feed = SyndicationFeed.Load(reader);
        }
        catch (System.Net.WebException ex)
        {
            Console.WriteLine("WebException in ReadFeed: " + ex.Message);
            return;
        }
        
        bool latestItem = true;
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
            if (SkipCategories.Contains(item.Categories[0].Name))
                continue;
            
            string[] channels = Nyaa.PatternMatch(item.Title.Text);
            if (channels != null)
            {
                foreach (string channel in channels)
                    irc.SendMessage(channel, string.Format("号外! 号外! 号外! \u0002:: {0} ::\u000F {1}",
                                                           item.Title.Text, item.Id));
            }
        }
        lastPrintedTime = latestPublish;
    }
}


class Config : XmlConfig
{
    public int Interval { get; set; }
    public HashSet<string> SkipCategories { get; set; }


    public Config(string file) : base(file)
    {}

    public override void LoadConfig()
    {
        Interval = (int)Config.Element("interval");

        SkipCategories = new HashSet<string>();
        XElement skipCategories = Config.Element("skipcategories");
        if (skipCategories != null)
        {
            foreach (XElement cat in skipCategories.Elements())
            {
                if (!string.IsNullOrEmpty(cat.Value))
                    SkipCategories.Add(cat.Value);
            }
        }
    }

    public override XElement DefaultConfig()
    {
        var config =
            new XElement("config",
                         new XElement("interval", 15, new XComment("In minutes")),
                         new XElement("skipcategories",
                            new XElement("category", "Non-English-translated Anime"),
                            new XElement("category", "Non-English-translated Live Action"),
                            new XElement("category", "Non-English-scanlated Books")
                            )
                         );
        return config;
    }
}