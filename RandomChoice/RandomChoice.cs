using System;
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
        get { return "1.2"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"c", "c <options> - Takes either a range of numbers (.c x-y) or a list of options seperated by" +
                     @" ""or""/"","". If the list of options contains neither, it seperates the options by space."},

                {"cd", "cd [seconds] - Want to simulwatch something? Countdown is the tool for you! Invoking .cd " +
                    "will provide you with an automatic countdown (default/min: 3s, max: 10s) " +
                    "and end in a spectacular launch!"},

                {"8ball", "8ball [question] - Ask the Magic 8-Ball any yes or no question."}
            };
        }
    }


    public void Stop()
    {}

    [ImportingConstructor]
    public IrcRandom(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        conf = new Config(meidoComm.ConfDir + "/RandomChoice.xml");

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        string index0 = e.MessageArray[0];

        if (index0 == Prefix + "c")
        {
            string choice = RandomChoice.RndChoice(e.MessageArray);
            if (choice != null)
                irc.SendMessage(e.Channel, "{0}: {1}", e.Nick, choice);
        }
        else if (index0 == Prefix + "cd")
            Countdown(e);

        else if (index0 == Prefix + "8ball")
            new Thread( () => EightBall(e.Channel) ).Start();
    }


    void Countdown(IIrcMessage e)
    {
        const int maxCountdownSec = 10;
        const int stdCountdownSec = 3;
        int tminus;
        if ( e.MessageArray.Length == 2 && int.TryParse(e.MessageArray[1], out tminus) )
        {
            if (tminus >= stdCountdownSec && tminus <= maxCountdownSec)
                ThreadPool.QueueUserWorkItem( (data) => IrcCountdown(e.Channel, tminus) );
        }
        else
            ThreadPool.QueueUserWorkItem( (data) => IrcCountdown(e.Channel, stdCountdownSec) );
    }


    void IrcCountdown(string channel, int seconds)
    {
        string launch = RandomChoice.ChooseRndItem(conf.LaunchChoices);

        irc.SendMessage(channel, "Commencing Countdown");
        Thread.Sleep(500);
        for (int tminus = seconds; tminus > 0; tminus--)
        {
            irc.SendMessage(channel, tminus.ToString());
            Thread.Sleep(1000);
        }
        irc.SendMessage(channel, launch);
    }


    void EightBall(string channel)
    {
        string choice = RandomChoice.Shake8Ball();

        irc.SendMessage(channel, "The Magic 8-Ball says...");
        // Wait for 1.5 seconds.
        Thread.Sleep(1500);
        irc.SendMessage(channel, choice + ".");
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


    public static T ChooseRndItem<T>(List<T> items)
    {
        lock (rnd)
        {
            int rndIndex = rnd.Next(items.Count);
            return items[rndIndex];
        }
    }

    public static string Shake8Ball()
    {
        lock (rnd)
        {
            int rndIndex = rnd.Next(ballChoices.Length);
            return ballChoices[rndIndex];
        }
    }

    static List<string> ConstructOptions(string[] message)
    {
        var options = new List<string>();
        var tempOption = new List<string>();

        string word;
        // Start at index 1 because the array we got passed contains ".c" at index 0.
        for (int i = 1; i < message.Length; i++)
        {
            word = message[i];

            if ( word.Equals("or", StringComparison.OrdinalIgnoreCase) )
            {
                if (tempOption.Count != 0)
                {
                    options.Add( string.Join(" ", tempOption) );
                    tempOption.Clear();
                }
            }
            else if ( word.EndsWith(",") )
            {
                string removedComma = word.Substring(0, word.Length - 1);
                if (removedComma.Length != 0)
                {
                    tempOption.Add(removedComma);
                    options.Add( string.Join(" ", tempOption) );
                    tempOption.Clear();
                }
            }
            else if ( !string.IsNullOrWhiteSpace(word) )
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
                    int rndInt = rnd.Next( begin, (end + 1) );
                    return rndInt.ToString();
                }
            }
        }
        // Else assume that it's a collection of options, so extract those options into an array and choose
        // a random member.
        else
        {
            List<string> options = ConstructOptions(message);
            return ChooseRndItem(options);
        }
    }
}


class Config : XmlConfig
{
    public List<string> LaunchChoices { get; set; }


    public Config(string file) : base(file)
    {}

    public override void LoadConfig()
    {
        XElement countdownOptions = Config.Element("countdown");

        LaunchChoices = new List<string>();
        foreach (XElement option in countdownOptions.Elements())
            LaunchChoices.Add(option.Value);
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