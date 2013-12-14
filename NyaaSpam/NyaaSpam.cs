using System;
using System.IO;
using System.Threading;
using System.Xml;
using System.ServiceModel.Syndication;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class NyaaSpam : IMeidoHook
{
    IIrcComm irc;
    NyaaPatterns nyaa = new NyaaPatterns();

    HashSet<string> _skipCategories = new HashSet<string>(new string[] {
        "Non-English-translated Anime", "Non-English-translated Live Action", "Non-English-scanlated Books"});
    public HashSet<string> SkipCategories
    {
        get { return _skipCategories; }
        set { _skipCategories = value; }
    }

    public string Description
    {
        get { return "NyaaSpam v0.33"; }
    }

    public Dictionary<string,string> exportedHelp
    {
        get 
        {
            return new Dictionary<string, string>()
            {};
        }
    }


    [ImportingConstructor]
    public NyaaSpam(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddChannelMessageHandler(HandleMessage);

        string nyaaFile = AppDomain.CurrentDomain.BaseDirectory + "/_nyaa";
        try
        {
            nyaa.LoadFromFile(nyaaFile);
        }
        catch (FileNotFoundException)
        {
            File.Create(nyaaFile).Dispose();
            nyaa.LoadFromFile(nyaaFile);
        }

        new Thread(ReadFeed).Start();
    }

    public void HandleMessage(IIrcMessage e)
    {
        if (e.Message.StartsWith(".nyaa add ") && e.MessageArray.Length > 2)
            Add(e.Channel, e.Message.Substring(10));

        else if (e.Message.StartsWith(".nyaa del ") && e.MessageArray.Length > 2)
            Del(e.Channel, e.Nick, e.Message.Substring(10));

        else if (e.Message.StartsWith(".nyaa show"))
        {
            if (e.MessageArray.Length > 2)
                Show(e.Channel, e.Nick, e.Message.Substring(11));
            else
                Show(e.Channel, e.Nick);
        }

        else if (e.Message.StartsWith(".nyaa reload"))
        {
            nyaa.ReloadFile();
            irc.SendMessage(e.Channel, "Patterns reloaded from disk.");
        }
        else if (e.Message.StartsWith(".nyaa save"))
        {
            nyaa.WriteFile();
            irc.SendMessage(e.Channel, "Patterns saved to disk.");
        }
    }

    void Add(string channel, string patternsStr)
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
            Thread.Sleep(TimeSpan.FromMinutes(15));
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
                if (latestItem == true)
                {
                    latestPublish = item.PublishDate;
                    latestItem = false;
                }

                // Once we hit items that we probably already have printed, stop processing the rest.
                if (item.PublishDate <= lastPrintedTime)
                    break;

                // Debug
                Console.WriteLine(item.Categories[0].Name);
                Console.WriteLine(item.Title.Text);

                if (SkipCategories.Contains(item.Categories[0].Name))
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