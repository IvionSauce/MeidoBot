using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
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
        get { return "0.60"; }
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
        nyaa.Dispose();
    }

    [ImportingConstructor]
    public NyaaSpam(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        string nyaaFile = meidoComm.ConfDir + "/_nyaapatterns.xml";
        try
        {
            nyaa.Deserialize(nyaaFile);
        }
        catch (FileNotFoundException)
        {}
        nyaa.BufferTime = TimeSpan.FromMinutes(1);

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
            {
                irc.SendMessage( e.Channel, string.Format("See \"{0}h nyaa <add|del|show>\" for help.", Prefix) );
                return;
            }

            string command = e.MessageArray[1];
            string input = "";
            int? assocPat = null;

            // nyaa ex <add|del|show>
            // nyaa ex <assocPat> <add|del|show>
            // Command will be reassigned.
            if (command == "ex" && e.MessageArray.Length > 2)
            {
                int assocPatInt;
                // Exclude Patterns associated with a pattern.
                if (int.TryParse(e.MessageArray[2], out assocPatInt) && e.MessageArray.Length > 3)
                {
                    assocPat = assocPatInt;
                    command = e.MessageArray[3];

                    if (e.MessageArray.Length > 4)
                        input = string.Join(" ", e.MessageArray, 4, e.MessageArray.Length - 4);
                }
                // Global Exclude Patterns.
                else
                {
                    assocPat = -1;
                    command = e.MessageArray[2];

                    if (e.MessageArray.Length > 3)
                        input = string.Join(" ", e.MessageArray, 3, e.MessageArray.Length - 3);
                }
            }
            // nyaa <add|del|show>
            // Command already set above.
            else if (e.MessageArray.Length > 2)
                input = string.Join(" ", e.MessageArray, 2, e.MessageArray.Length - 2);


            if (command == "add")
            {
                Add(e.Channel, e.Nick, input, assocPat);
            }
            else if (command == "del")
            {
                Del(e.Channel, e.Nick, input, assocPat);
            }
            else if (command == "show")
            {
                ShowAll(e.Channel, e.Nick, assocPat);
            }
        }
    }

    void Add(string channel, string nick, string patternsStr, int? assocPat)
    {
        var patterns = new List<string>();
        // If enclosed in quotation marks, add string within the marks verbatim.
        if ( patternsStr.StartsWith("\"") && patternsStr.EndsWith("\"") )
        {
            // Slice off the quotation marks.
            string pattern = patternsStr.Substring(1, patternsStr.Length - 2);
            patterns.Add(pattern);
        }
        // Else interpret comma's as seperators between different titles.
        else
        {
            foreach ( string pattern in patternsStr.Split(',') )
                patterns.Add( pattern.Trim() );
        }

        int amount = 0;
        if (assocPat == null)
        {
            foreach (var pattern in patterns)
                if (nyaa.Add(channel, pattern) != -1)
                    amount++;

            irc.SendNotice( nick, string.Format("Added {0} pattern(s)", amount) );
        }
        else if (assocPat >= 0)
        {
            foreach (var pattern in patterns)
                if (nyaa.AddExclude(channel, assocPat.Value, pattern) != -1)
                    amount++;

            irc.SendNotice( nick, string.Format("Added {0} exclude pattern(s)", amount) );
        }
        else
        {
            foreach (var pattern in patterns)
                if (nyaa.AddGlobalExclude(channel, pattern) != -1)
                    amount++;

            irc.SendNotice( nick, string.Format("Added {0} global exclude pattern(s)", amount) );
        } 
    }


    void Del(string channel, string nick, string numbersStr, int? assocPat)
    {
        int[] numbers = GetNumbers(numbersStr);
        Array.Reverse(numbers);

        string removedPattern;
        if (assocPat == null)
        {
            foreach (int n in numbers)
            {
                removedPattern = nyaa.Remove(channel, n);
                if (removedPattern != null)
                    irc.SendNotice( nick, string.Format("Deleted pattern: {0}", removedPattern) );
            }
        }
        else if (assocPat >= 0)
        {
            foreach (int n in numbers)
            {
                removedPattern = nyaa.RemoveExclude(channel, assocPat.Value, n);
                if (removedPattern != null)
                    irc.SendNotice( nick, string.Format("Removed exclude pattern: {0}", removedPattern) );
            }
        }
        else
        {
            foreach (int n in numbers)
            {
                removedPattern = nyaa.RemoveGlobalExclude(channel, n);
                if (removedPattern != null)
                    irc.SendNotice( nick, string.Format("Removed global exclude pattern: {0}", removedPattern) );
            }
        }
        irc.SendNotice(nick, " -----");
    }


    /* void Show(string channel, string nick, string numbersStr)
    {
        int[] numbers = GetNumbers(numbersStr);
        var patterns = new List<string>();

        string pat;
        foreach (int n in numbers)
        {
            pat = nyaa.Get(channel, n);
            if (pat != null)
                patterns.Add(pat);
        }

        IrcShow(nick, patterns.ToArray());
    } */

    void ShowAll(string channel, string nick, int? assocPat)
    {
        string[] patterns;
        if (assocPat == null)
        {
            irc.SendNotice( nick, string.Format("Patterns for {0}:", channel) );
            
            patterns = nyaa.GetPatterns(channel);
            IrcShow(nick, patterns);
        }
        else if (assocPat >= 0)
        {
            string pattern = nyaa.Get(channel, assocPat.Value);
            if (pattern != null)
            {
                irc.SendNotice( nick, string.Format("Exclude patterns associated with \"{0}\":", pattern) );

                patterns = nyaa.GetExcludePatterns(channel, assocPat.Value);
                IrcShow(nick, patterns);
            }
        }
        else
        {
            irc.SendNotice( nick, string.Format("Global exclude patterns for {0}:", channel) );

            patterns = nyaa.GetGlobalExcludePatterns(channel);
            IrcShow(nick, patterns);
        }
    }

    void IrcShow(string nick, string[] patterns)
    {
        if (patterns.Length > 0)
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
        if (string.IsNullOrWhiteSpace(numbersStr))
            return new int[0];

        var numbers = new List<int>();

        int num;
        foreach (string s in numbersStr.Split(','))
        {
            if (s.Contains("-"))
                numbers.AddRange( GetRange(s) );
            else if (int.TryParse(s, out num))
                numbers.Add(num);
        }

        numbers.Sort();
        return numbers.ToArray();
    }

    // Return list of ints for a range given as "x-y".
    List<int> GetRange(string rangeStr)
    {
        var numbers = new List<int>();

        string[] startEnd = rangeStr.Split('-');
        if (startEnd.Length != 2)
            return numbers;

        int start, end;

        if ( int.TryParse(startEnd[0], out start) && int.TryParse(startEnd[1], out end) )
        {
            int num = start;
            while (num <= end)
            {
                numbers.Add(num);
                num++;
            }
        }
        return numbers;
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