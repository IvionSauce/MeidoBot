using System;
using System.Collections.Generic;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class NyaaSpam : IMeidoHook
{
    public string Name
    {
        get { return "NyaaSpam"; }
    }
    public string Version
    {
        get { return "0.81"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"nyaa add", "nyaa add <pattern...> - Adds pattern(s), seperated by \",\". Unless enclosed in " +
                    "quotation marks (\"), in which case the pattern is added verbatim. (Ex: nyaa add show1, show2)"},
                
                {"nyaa del", "nyaa del <index...>|<pattern...> - Removes pattern(s) inidicated by list of indices. " +
                    "Can also accepts a list of patterns (or parts thereof) to be deleted. " +
                    "List of indices/patterns are seperated by \",\" and indices can specified as a " +
                    "number range \"x-y\". (Ex: nyaa del 4, 7, 0-2)"},
                
                {"nyaa show", "nyaa show - Gives an overview of all patterns that are checked for."},

                {"nyaa bangs", "Shows all NyaaBangs that are currently supported. These are shorthands that can be " +
                    "used in patterns."}
            };
        }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    readonly IIrcComm irc;
    readonly IMeidoComm meido;
    readonly ILog log;

    volatile Config conf;

    Patterns feedPatterns;
    FeedReader feedReader;

    const string feedError = "Disabled due to invalid or missing feed address.";


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
            new Trigger("nyaa", Nyaa, TriggerOption.ChannelOnly)
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


    public void Nyaa(ITriggerMsg e)
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

                case "bangs":
                Bangs(e.Nick);
                return;
            }
        }
        else
            e.Reply("Access denied, please contact my owner for information.");
    }


    void Add(string channel, string nick, string patternsStr, int? assocPat)
    {
        var patterns = InputTools.GetPatterns(patternsStr);

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


    void Del(string channel, string nick, string deletions, int? assocPat)
    {
        var numbers = InputTools.GetNumbers(deletions);
        // If we got no numbers, assume it's a list of patterns (or parts thereof) to be deleted.
        // Search include patterns (no search for excludes) for pattern by their substrings.
        if (assocPat == null && numbers.Count == 0)
        {
            var delPatterns = InputTools.GetPatterns(deletions);
            numbers = feedPatterns.Search(channel, delPatterns);
            // Search returns results in the order they were requested, so we need to sort.
            numbers.Sort();
        }
        numbers = InputTools.PrepareDeletions(numbers);

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
            foreach (string line in OutputTools.TwoColumns(patterns))
            {
                irc.SendNotice(nick, line);
            }
        }
        irc.SendNotice(nick, " -----");
    }


    void Bangs(string nick)
    {
        irc.SendNotice(nick, "Supported NyaaBang shorthands:");
        foreach (string desc in BangShorthands.GetDescriptions())
        {
            irc.SendNotice(nick, desc);
        }
        irc.SendNotice(nick, " -----");
    }
}