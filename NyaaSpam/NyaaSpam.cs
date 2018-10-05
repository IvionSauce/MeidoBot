using System;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class NyaaSpam : IMeidoHook
{
    readonly IIrcComm irc;
    readonly IMeidoComm meido;
    readonly ILog log;

    volatile Config conf;

    Patterns feedPatterns;
    FeedReader feedReader;

    const string feedError = "Disabled due to invalid or missing feed address.";


    public string Name
    {
        get { return "NyaaSpam"; }
    }
    public string Version
    {
        get { return "0.80"; }
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
                {"nyaa show", "show - Gives an overview of all patterns that are checked for."}
            };
        }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    public void Stop()
    {
        if (feedReader != null)
            feedReader.Stop();
        
        if (feedPatterns != null)
            feedPatterns.Dispose();
    }

    [ImportingConstructor]
    public NyaaSpam(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        meido = meidoComm;
        irc = ircComm;
        log = meido.CreateLogger(this);

        // Setting up configuration.
        var xmlConf = new XmlConfig2<Config>(
            Config.DefaultConfig(),
            (xml) => new Config(xml),
            log,
            Configure
        );
        meido.LoadAndWatchConfig("NyaaSpam.xml", xmlConf);

        Triggers = new Trigger[] {
            new Trigger("nyaa", Nyaa, TriggerOptions.ChannelOnly)
        };
    }

    void Configure(Config config)
    {
        // Let's stop the reader asap, just to be safe.
        if (feedReader != null)
            feedReader.Stop();
        
        conf = config;
        if (conf.Feed != null)
        {
            SetupPatterns();
            SetupReader();
        }
        else
            log.Error(feedError);
    }

    void SetupPatterns()
    {
        if (feedPatterns == null)
        {
            string patternsFile = meido.DataPathTo("nyaapatterns.xml");
            feedPatterns = new Patterns( TimeSpan.FromMinutes(1) ) { FileLocation = patternsFile };
            try
            {
                feedPatterns.Deserialize();
            }
            catch (System.IO.FileNotFoundException)
            {}
        }
    }

    void SetupReader()
    {
        if (feedReader == null)
        {
            var dtFile = new DateTimeFile( meido.DataPathTo("nyaa-last") );
            feedReader = new FeedReader(irc, log, dtFile, feedPatterns);
        }

        feedReader.Configure(conf);
        feedReader.Start();
    }


    public void Nyaa(IIrcMessage e)
    {
        // Some early return conditions.
        if (conf.Feed == null)
        {
            e.Reply(feedError);
            return;
        }
        if (e.MessageArray.Length == 1)
        {
            e.Reply("Currently fetching {0} every {1} minutes. See nyaa add|del|show for usage.",
                    conf.Feed, conf.Interval);

            return;
        }

        // The real deal.
        if (conf.ActiveChannels.Contains(e.Channel) || meido.AuthLevel(e.Nick) >= 2)
        {
            string command = e.MessageArray[1];
            string input = string.Empty;
            int? assocPat = null;

            // Command will be reassigned.
            if (command == "ex" && e.MessageArray.Length > 2)
            {
                int assocPatInt;
                // nyaa ex <assocPat> <add|del|show>
                // Exclude Patterns associated with a pattern.
                if (int.TryParse(e.MessageArray[2], out assocPatInt) && e.MessageArray.Length > 3)
                {
                    assocPat = assocPatInt;
                    command = e.MessageArray[3];

                    if (e.MessageArray.Length > 4)
                        input = string.Join(" ", e.MessageArray, 4, e.MessageArray.Length - 4);
                }
                // nyaa ex <add|del|show>
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


            switch (command)
            {
            case "add":
                Add(e.Channel, e.Nick, input, assocPat);
                return;
            case "del":
                Del(e.Channel, e.Nick, input, assocPat);
                return;
            case "show":
                ShowAll(e.Channel, e.Nick, assocPat);
                return;
            }
        }
        else
            e.Reply("Access denied, please contact my owner for information.");
    }


    void Add(string channel, string nick, string patternsStr, int? assocPat)
    {
        var patterns = GetPatterns(patternsStr);

        int amount = 0;
        if (assocPat == null)
        {
            foreach (var pattern in patterns)
                if (feedPatterns.Add(channel, pattern) != -1)
                    amount++;

            irc.SendNotice(nick, "Added {0} pattern(s)", amount);
        }
        else if (assocPat >= 0)
        {
            foreach (var pattern in patterns)
                if (feedPatterns.AddExclude(channel, assocPat.Value, pattern) != -1)
                    amount++;

            irc.SendNotice(nick, "Added {0} exclude pattern(s)", amount);
        }
        else
        {
            foreach (var pattern in patterns)
                if (feedPatterns.AddGlobalExclude(channel, pattern) != -1)
                    amount++;

            irc.SendNotice(nick, "Added {0} global exclude pattern(s)", amount);
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
                removedPattern = feedPatterns.Remove(channel, n);
                if (removedPattern != null)
                    irc.SendNotice(nick, "Deleted pattern: {0}", removedPattern);
            }
        }
        else if (assocPat >= 0)
        {
            foreach (int n in numbers)
            {
                removedPattern = feedPatterns.RemoveExclude(channel, assocPat.Value, n);
                if (removedPattern != null)
                    irc.SendNotice(nick, "Removed exclude pattern: {0}", removedPattern);
            }
        }
        else
        {
            foreach (int n in numbers)
            {
                removedPattern = feedPatterns.RemoveGlobalExclude(channel, n);
                if (removedPattern != null)
                    irc.SendNotice(nick, "Removed global exclude pattern: {0}", removedPattern);
            }
        }
        irc.SendNotice(nick, " -----");
    }


    void ShowAll(string channel, string nick, int? assocPat)
    {
        string[] patterns;
        if (assocPat == null)
        {
            irc.SendNotice(nick, "Patterns for {0}:", channel);
            patterns = feedPatterns.GetPatterns(channel);
        }
        else if (assocPat >= 0)
        {
            string pattern = feedPatterns.Get(channel, assocPat.Value);
            if (pattern != null)
                irc.SendNotice(nick, "Exclude patterns associated with \"{0}\":", pattern);

            patterns = feedPatterns.GetExcludePatterns(channel, assocPat.Value);
        }
        else
        {
            irc.SendNotice(nick, "Global exclude patterns for {0}:", channel);
            patterns = feedPatterns.GetGlobalExcludePatterns(channel);
        }

        IrcShow(nick, patterns);
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
                    irc.SendNotice( nick, "[{0}] {1,-30} [{2}] {3}", i, patterns[i], j, patterns[j] );
                else
                    irc.SendNotice( nick, "[{0}] {1}", i, patterns[i] );
            }
        }
        
        irc.SendNotice(nick, " -----");
    }


    static List<string> GetPatterns(string patternsStr)
    {
        const string quot = "\"";
        
        var patterns = new List<string>();
        // If enclosed in quotation marks, add string within the marks verbatim.
        if ( patternsStr.StartsWith(quot, StringComparison.OrdinalIgnoreCase) &&
            patternsStr.EndsWith(quot, StringComparison.OrdinalIgnoreCase) )
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
        
        return patterns;
    }


    static int[] GetNumbers(string numbersStr)
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
    static List<int> GetRange(string rangeStr)
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