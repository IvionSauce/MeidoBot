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
    NyaaConfig conf;

    public string Prefix { get; set; }

    public string Name
    {
        get { return "NyaaSpam"; }
    }
    public string Version
    {
        get { return "0.42"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"nyaa", "See \"nyaa add|del|show\" for help."},
                {"nyaa add", "add <pattern...> - Adds pattern(s), seperated by \",\". Unless enclosed in quotation " +
                    "marks (\"), in which case the pattern is added verbatim. (Ex: nyaa add show1, show2)"},
                {"nyaa del", "del <index...> - Removes pattern(s) inidicated by given indices. Can be seperated by " +
                    "\",\" and accepts ranges given as \"x-y\". (Ex: nyaa del 4, 7, 0-2)"},
                {"nyaa show", "show [index...] - Gives an overview of all patterns that are checked for. If given " +
                    "an index/indices it will show just those. (Ex: nyaa show) (Ex: nyaa show 4, 7, 0-2)"}
            };
        }
    }


    [ImportingConstructor]
    public NyaaSpam(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        conf = new NyaaConfig(meidoComm.ConfDir + "/NyaaSpam.xml");

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

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleMessage);

        new Thread(ReadFeed).Start();
    }

    public void HandleMessage(IIrcMessage e)
    {
        if (e.Message.StartsWith(Prefix + "nyaa add ") && e.MessageArray.Length > 2)
        {
            Add( e.Channel, string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2) );
        }

        else if (e.Message.StartsWith(Prefix + "nyaa del ") && e.MessageArray.Length > 2)
        {
            Del( e.Channel, e.Nick, string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2) );
        }

        else if (e.Message.StartsWith(Prefix + "nyaa show"))
        {
            if (e.MessageArray.Length > 2)
                Show( e.Channel, e.Nick, string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2) );
            else
                Show(e.Channel, e.Nick);
        }

        else if (e.Message.StartsWith(Prefix + "nyaa reload"))
        {
            nyaa.ReloadFile();
            irc.SendMessage(e.Channel, "Patterns reloaded from disk.");
        }
        else if (e.Message.StartsWith(Prefix + "nyaa save"))
        {
            nyaa.WriteFile();
            irc.SendMessage(e.Channel, "Patterns saved to disk.");
        }
    }

    void Add(string channel, string patternsStr)
    {
        // If enclosed in quotation marks, add string within the marks verbatim.
        if ( patternsStr.StartsWith("\"") && patternsStr.EndsWith("\"") )
        {
            // Slice off the quotation marks.
            string pattern = patternsStr.Substring(1, patternsStr.Length - 2);
            nyaa.Add(channel, pattern);
        }
        // Else interpret comma's as seperators between different titles.
        else
        {
            string[] patterns = patternsStr.Split(',');
            foreach (string pattern in patterns)
            {
                if (pattern[0] == ' ')
                    nyaa.Add(channel, pattern.Substring(1));
                else
                    nyaa.Add(channel, pattern);
            }
        }
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
                irc.SendNotice(nick, string.Format("Deleted: {0}", removedPattern));
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
                irc.SendNotice(nick, string.Format("[{0}] \"{1}\"", i, pattern));
        }
        if (pattern != null)
            irc.SendNotice(nick, " -----");
    }

    void Show(string channel, string nick)
    {
        irc.SendNotice(nick, string.Format("Patterns for {0}:", channel));

        string[] patterns = nyaa.GetPatterns(channel);
        if (patterns == null)
            return;

        int half = (int)Math.Ceiling(patterns.Length / 2d);

        int j;
        for (int i = 0; i < half; i++)
        {
            j = i + half;
            if (j < patterns.Length)
                irc.SendNotice(nick, string.Format("[{0}] {1,-30} [{2}] {3}", i, patterns[i], j, patterns[j]));
            else
                irc.SendNotice(nick, string.Format("[{0}] {1}", i, patterns[i]));
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

    void ReadFeed()
    {
        DateTimeOffset lastPrintedTime = DateTimeOffset.Now;
        DateTimeOffset latestPublish = DateTimeOffset.Now;

        while (true)
        {
            Thread.Sleep( TimeSpan.FromMinutes(conf.Interval) );
            Console.WriteLine("\n{0}: Starting ReadFeed Cycle", DateTime.Now);

            if (nyaa.ChangedSinceLastSave() == true)
                nyaa.WriteFile();

            SyndicationFeed feed;
            try
            {
                XmlReader reader = XmlReader.Create("http://www.nyaa.se/?page=rss");
                feed = SyndicationFeed.Load(reader);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in ReadFeed: " + ex.Message);
                continue;
            }

            bool latestItem = true;
            foreach (SyndicationItem item in feed.Items)
            {
                // Since feed.Items is only IEnumerable, we can't just access the first member by index, so we have to
                // use this roundabout way. :/
                if (latestItem == true)
                {
                    latestPublish = item.PublishDate;
                    latestItem = false;
                }

                // Once we hit items that we probably already have printed, stop processing the rest.
                if (item.PublishDate <= lastPrintedTime)
                    break;

                // Skip processing items in categories we don't care about.
                if (conf.SkipCategories.Contains(item.Categories[0].Name))
                    continue;

                string[] channels = nyaa.PatternMatch(item.Title.Text);
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
}


class NyaaConfig : XmlConfig
{
    public int Interval { get; set; }
    public HashSet<string> SkipCategories { get; set; }


    public NyaaConfig(string file) : base(file)
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