using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcRandom : IMeidoHook
{
    IIrcComm irc;

    public string Description
    {
        get { return "RandomChoice v0.93"; }
    }

    public Dictionary<string,string> exportedHelp
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {".c", ".c <options> - Takes either a range of numbers (.c x-y) or a list of options seperated by" +
                     @" ""or""/"","". If the list of options contains neither, it seperates the options by space."},

                {".cd", ".cd - Want to simulwatch something? Countdown is the tool for you! Invoking .cd will provide " +
                    "you with an automatic countdown, starting from 3 and ending in a spectacular launch!"},

                {".8ball", ".8ball [question] - Ask the Magic 8-Ball any yes or no question."}
            };
        }
    }


    [ImportingConstructor]
    public IrcRandom(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        switch (e.MessageArray[0])
        {
        case ".c":
            string choice = RandomChoice.RndChoice(e.MessageArray);
            if (choice != null)
                irc.SendMessage(e.Channel, e.Nick + ": " + choice);
            break;
        case ".cd":
            new Thread( () => Countdown(e.Channel) ).Start();
            break;
        case ".8ball":
            new Thread( () => EightBall(e.Channel) ).Start();
            break;
        }
    }

    void Countdown(string channel)
    {
        string launch = RandomChoice.ChooseRndLaunch();

        irc.SendMessage(channel, "Commencing Countdown");
        Thread.Sleep(500);
        irc.SendMessage(channel, "3");
        Thread.Sleep(1000);
        irc.SendMessage(channel, "2");
        Thread.Sleep(1000);
        irc.SendMessage(channel, "1");
        Thread.Sleep(1000);
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
    static Random rnd = new Random();

    static readonly string[] ballChoices = {"It is certain", "It is decidedly so", "Without a doubt", "Yes definitely",
        "You may rely on it", "As I see it yes", "Most likely", "Outlook good", "Yes", "Signs point to yes",
        "Reply hazy try again", "Ask again later", "Better not tell you now", "Cannot predict now",
        "Concentrate and ask again", "Don't count on it", "My reply is no", "My sources say no", "Outlook not so good",
        "Very doubtful"};

    static readonly string[] launchChoices = {"Launch!", "Hasshin!", "Gattai!", "Gasshin!", "Rider Kick!", "Clock Up!",
        "Are you Ready? We are l@dy!", "Heaven or Hell!", "Apprivoise!!", "Kiraboshi!", "Fight!"};


    public static string ChooseRndLaunch()
    {
        int rndIndex = rnd.Next(launchChoices.Length);
        return launchChoices[rndIndex];
    }

    public static string Shake8Ball()
    {
        int rndIndex = rnd.Next(ballChoices.Length);
        return ballChoices[rndIndex];
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

            if (word.ToLower() == "or")
            {
                if (tempOption.Count != 0)
                {
                    options.Add( string.Join(" ", tempOption.ToArray()) );
                    tempOption.Clear();
                }
            }
            else if (word.EndsWith(","))
            {
                string removedComma = word.Substring(0, word.Length - 1);
                if (removedComma.Length != 0)
                    tempOption.Add(removedComma);

                if (tempOption.Count != 0)
                {
                    options.Add( string.Join(" ", tempOption.ToArray()) );
                    tempOption.Clear();
                }
            }
            else if (!string.IsNullOrWhiteSpace(word))
                tempOption.Add(word);
        }

        // If there are no ","s or "or"s, and hence just one option (stored in tempOption), assume that the options
        // are delimited by spaces. Return options as array.
        // Otherwise return collected options as an array.
        if (options.Count == 0)
            return tempOption.ToArray();

        // Clean-up last option, if applicable.
        if (tempOption.Count != 0)
            options.Add( string.Join(" ", tempOption.ToArray()) );
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
                begin = Convert.ToInt32(numberSeq.Groups[1].Value);
                end = Convert.ToInt32(numberSeq.Groups[2].Value);
            }
            catch (OverflowException)
            {
                return null;
            }

            if (begin > end)
                return null;
            else
                return Convert.ToString(rnd.Next(begin, end));
        }
        // Else assume that it's a collection of options, so extract those options into an array and choose
        // a random member.
        else
        {
            string[] options = ConstructOptions(message);
            int rndIndex = rnd.Next(options.Length);
            return options[rndIndex];
        }
    }
}