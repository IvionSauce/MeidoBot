using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using IvionSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcRandom : IMeidoHook, IPluginTriggers
{
    public string Name
    {
        get { return "RandomChoice"; }
    }
    public string Version
    {
        get { return "1.25"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    readonly IIrcComm irc;
    volatile Config conf;


    public void Stop()
    {}

    [ImportingConstructor]
    public IrcRandom(IIrcComm ircComm, IMeidoComm meido)
    {
        var threading = TriggerThreading.Threadpool;
        Triggers = new Trigger[] {
            
            new Trigger(Choose, "choose", "decide", "d") {
                Help = new TriggerHelp(
                    "<option...>",
                    @"Takes a list of options separated by "","" and/or ""or"". " +
                    "If the list of options contains neither, then options will be separated by space.")
            },

            new Trigger(Countdown, threading, "countdown", "cd") {
                Help = new TriggerHelp(
                    "[seconds]",
                    "Want to simulwatch something? Countdown is the tool for you! Invoking this will provide you " +
                    "with an automatic countdown (default/min: 3s, max: 10s) and a spectacular launch!")
            },

            new Trigger(EightBall, threading, "8ball") {
                Help = new TriggerHelp(
                    "[question]",
                    "Ask the Magic 8-Ball any yes or no question.")
            }
        };

        // Setting up configuration.
        var xmlConf = new XmlConfig2<Config>(
            Config.DefaultConfig(),
            (xml) => new Config(xml),
            meido.CreateLogger(this),
            Configure
        );
        meido.LoadAndWatchConfig("RandomChoice.xml", xmlConf);
        irc = ircComm;
    }

    void Configure(Config config)
    {
        conf = config;
    }


    void Choose(ITriggerMsg e)
    {
        string choice = RandomChoice.RndChoice(e.MessageArray);
        if (choice != null)
            e.Reply(choice);
    }


    void Countdown(ITriggerMsg e)
    {
        const int maxCountdownSec = 10;
        const int minCountdownSec = 3;
        int tminus;
        // cd [seconds]
        if ( e.MessageArray.Length == 2 && int.TryParse(e.MessageArray[1], out tminus) )
        {
            if (tminus >= minCountdownSec && tminus <= maxCountdownSec)
                Countdown(e.ReturnTo, tminus);
        }
        // cd
        else
            Countdown(e.ReturnTo, minCountdownSec);
    }

    void Countdown(string target, int seconds)
    {
        const string stdLaunch = "Liftoff!";
        string launch = RandomChoice.ChooseRndItem(conf.LaunchChoices) ?? stdLaunch;

        irc.SendMessage(target, "Commencing Countdown");
        Thread.Sleep(500);
        for (int tminus = seconds; tminus > 0; tminus--)
        {
            irc.SendMessage( target, tminus.ToString() );
            Thread.Sleep(1000);
        }
        irc.SendMessage(target, launch);
    }


    void EightBall(ITriggerMsg e)
    {
        string choice = RandomChoice.Shake8Ball();

        irc.SendMessage(e.ReturnTo, "The Magic 8-Ball says...");
        // Wait for 1.5 seconds.
        Thread.Sleep(1500);
        irc.SendMessage(e.ReturnTo, choice + ".");
    }
}


static class RandomChoice
{
    static readonly Random rnd = new Random();

    static readonly string[] ballChoices = {"It is certain", "It is decidedly so", "Without a doubt", "Yes definitely",
        "You may rely on it", "As I see it yes", "Most likely", "Outlook good", "Yes", "Signs point to yes",
        "Reply hazy try again", "Ask again later", "Better not tell you now", "Cannot predict now",
        "Concentrate and ask again", "Don't count on it", "My reply is no", "My sources say no", "Outlook not so good",
        "Very doubtful"};


    public static string ChooseRndItem(List<string> items)
    {
        if (items.Count > 0)
        {
            lock (rnd)
            {
                int rndIndex = rnd.Next(items.Count);
                return items[rndIndex];
            }
        }
        else
            return null;
    }

    public static string Shake8Ball()
    {
        lock (rnd)
        {
            int rndIndex = rnd.Next(ballChoices.Length);
            return ballChoices[rndIndex];
        }
    }


    public static string RndChoice(string[] message)
    {
        if (message.Length < 2)
            return null;
        
        // If first and only argument is a number sequence in the form of X-Y return a random int between X and Y.
        Match numberSeq = Regex.Match(message[1], @"^(\d+)-(\d+)$");
        if (numberSeq.Success && message.Length == 2)
        {
            int begin, end;
            try
            {
                begin = int.Parse(numberSeq.Groups[1].Value);
                end = int.Parse(numberSeq.Groups[2].Value);
                end++;
            }
            catch (OverflowException)
            {
                return null;
            }
            
            if (begin > end)
                return null;
            else
            {
                lock (rnd)
                {
                    int rndInt = rnd.Next(begin, end);
                    return rndInt.ToString();
                }
            }
        }
        // Else assume that it's a collection of options, so extract those options and choose a random member.
        else
        {
            List<string> options = ConstructOptions(message);
            return ChooseRndItem(options);
        }
    }


    static List<string> ConstructOptions(string[] message)
    {
        var options = new List<string>();
        var tempOption = new List<string>();

        // Start at index 1 because the array we got passed contains the trigger at index 0.
        for (int i = 1; i < message.Length; i++)
        {
            var word = message[i];

            if ( word.Equals("or", StringComparison.OrdinalIgnoreCase) )
            {
                if (tempOption.Count != 0)
                {
                    options.Add( string.Join(" ", tempOption) );
                    tempOption.Clear();
                }
            }
            else if ( word.EndsWith(",", StringComparison.OrdinalIgnoreCase) )
            {
                string removedComma = word.Substring(0, word.Length - 1);
                if (!removedComma.IsEmptyOrWhiteSpace())
                    tempOption.Add(removedComma);

                if (tempOption.Count != 0)
                {
                    options.Add( string.Join(" ", tempOption) );
                    tempOption.Clear();
                }
            }
            else if (!word.IsEmptyOrWhiteSpace())
                tempOption.Add(word);
        }

        // If there are no ","s or "or"s, and hence just one option (stored in tempOption), assume that the options
        // are delimited by spaces. Return options as array.
        // Otherwise return collected options as an array.
        if (options.Count == 0)
            return tempOption;

        // Clean-up last option, if applicable.
        if (tempOption.Count != 0)
            options.Add( string.Join(" ", tempOption) );
        return options;
    }

}