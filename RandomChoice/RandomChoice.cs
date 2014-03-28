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

    public string Prefix { get; set; }

    public string Name
    {
        get { return "RandomChoice"; }
    }
    public string Version
    {
        get { return "1.1"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"c", "c <options> - Takes either a range of numbers (.c x-y) or a list of options seperated by" +
                     @" ""or""/"","". If the list of options contains neither, it seperates the options by space."},

                {"cd", "cd [seconds] - Want to simulwatch something? Countdown is the tool for you! Invoking .cd will " +
                    "provide you with an automatic countdown (default: 3 seconds) and end in a spectacular launch!"},

                {"8ball", "8ball [question] - Ask the Magic 8-Ball any yes or no question."}
            };
        }
    }


    public void Stop()
    {}

    [ImportingConstructor]
    public IrcRandom(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        var conf = new Config(meidoComm.ConfDir + "/RandomChoice.xml");
        RandomChoice.LaunchChoices = conf.LaunchChoices.ToArray();

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
                irc.SendMessage(e.Channel, e.Nick + ": " + choice);
        }
        else if (index0 == Prefix + "cd")
        {
            const int maxCountdownSec = 10;
            const int stdCountdownSec = 3;
            int tminus;
            if ( e.MessageArray.Length == 2 && int.TryParse(e.MessageArray[1], out tminus) )
            {
                if (tminus >= stdCountdownSec && tminus <= maxCountdownSec)
                    new Thread( () => Countdown(e.Channel, tminus) ).Start();
            }
            else
                new Thread( () => Countdown(e.Channel, stdCountdownSec) ).Start();
        }

        else if (index0 == Prefix + "8ball")
            new Thread( () => EightBall(e.Channel) ).Start();
    }

    void Countdown(string channel, int seconds)
    {
        string launch = RandomChoice.ChooseRndLaunch();

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

    public static string[] LaunchChoices { get; set; }


    public static string ChooseRndLaunch()
    {
        lock (rnd)
        {
            int rndIndex = rnd.Next(LaunchChoices.Length);
            return LaunchChoices[rndIndex];
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

    static string[] ConstructOptions(string[] message)
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
            return tempOption.ToArray();

        // Clean-up last option, if applicable.
        if (tempOption.Count != 0)
            options.Add( string.Join(" ", tempOption) );
        return options.ToArray();
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
                    return Convert.ToString( rnd.Next(begin, (end + 1)) );
            }
        }
        // Else assume that it's a collection of options, so extract those options into an array and choose
        // a random member.
        else
        {
            string[] options = ConstructOptions(message);
            lock (rnd)
            {
                int rndIndex = rnd.Next(options.Length);
                return options[rndIndex];
            }
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