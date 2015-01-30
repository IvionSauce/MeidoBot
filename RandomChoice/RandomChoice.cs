using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using IvionSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcRandom : IMeidoHook
{
    readonly IIrcComm irc;
    readonly Config conf;


    public string Prefix { get; set; }

    public string Name
    {
        get { return "RandomChoice"; }
    }
    public string Version
    {
        get { return "1.22"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"c", "c <options> - Takes either a range of numbers (c x-y) or a list of options seperated by" +
                     @" ""or""/"","". If the list of options contains neither, it seperates the options by space."},

                {"cd", "cd [seconds] - Want to simulwatch something? Countdown is the tool for you! Invoking cd " +
                    "will provide you with an automatic countdown (default/min: 3s, max: 10s) " +
                    "and end in a spectacular launch!"},

                {"8ball", "8ball [question] - Ask the Magic 8-Ball any yes or no question."}
            };
        }
    }


    public void Stop()
    {}

    [ImportingConstructor]
    public IrcRandom(IIrcComm ircComm, IMeidoComm meido)
    {
        conf = new Config(Path.Combine(meido.ConfDir, "RandomChoice.xml"), meido.CreateLogger(this));

        irc = ircComm;
        irc.AddTriggerHandler(HandleTrigger);
    }

    public void HandleTrigger(IIrcMessage e)
    {
        switch(e.Trigger)
        {
        case "c":
            string choice = RandomChoice.RndChoice(e.MessageArray);
            if (choice != null)
                e.Reply(choice);
            return;
        case "cd":
            Countdown(e.ReturnTo, e.MessageArray);
            return;
        case "8ball":
            ThreadPool.QueueUserWorkItem( (data) => EightBall(e.ReturnTo) );
            return;
        }
    }


    void Countdown(string target, string[] message)
    {
        const int maxCountdownSec = 10;
        const int minCountdownSec = 3;
        int tminus;
        if ( message.Length == 2 && int.TryParse(message[1], out tminus) )
        {
            if (tminus >= minCountdownSec && tminus <= maxCountdownSec)
                ThreadPool.QueueUserWorkItem( (data) => IrcCountdown(target, tminus) );
        }
        else
            ThreadPool.QueueUserWorkItem( (data) => IrcCountdown(target, minCountdownSec) );
    }


    void IrcCountdown(string target, int seconds)
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


    void EightBall(string target)
    {
        string choice = RandomChoice.Shake8Ball();

        irc.SendMessage(target, "The Magic 8-Ball says...");
        // Wait for 1.5 seconds.
        Thread.Sleep(1500);
        irc.SendMessage(target, choice + ".");
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


class Config : XmlConfig
{
    public List<string> LaunchChoices { get; set; }


    public Config(string file, ILog log) : base(file, log) {}


    public override void LoadConfig()
    {
        XElement countdownOptions = Config.Element("countdown");
        LaunchChoices = new List<string>();

        if (countdownOptions != null)
        {
            foreach (XElement option in countdownOptions.Elements())
            {
                if (!string.IsNullOrWhiteSpace(option.Value))
                    LaunchChoices.Add(option.Value);
            }
        }
    }

    public override XElement DefaultConfig()
    {
        var config = 
            new XElement("config",
                         new XElement("countdown",
                            new XElement("option", "Launch!"),
                            new XElement("option", "Hasshin!"),
                            new XElement("option", "Gasshin!"),
                            new XElement("option", "Gattai!"),
                            new XElement("option", "Rider Kick!"),
                            new XElement("option", "Clock Up!"),
                            new XElement("option", "Are you Ready? We are l@dy!"),
                            new XElement("option", "Heaven or Hell!"),
                            new XElement("option", "Let's Rock!"),
                            new XElement("option", "Apprivoise!!"),
                            new XElement("option", "Kiraboshi!"),
                            new XElement("option", "Fight!")
                            )
                         );
        return config;
    }
}