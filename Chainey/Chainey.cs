using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Chainey;
using IvionSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class IrcChainey : IMeidoHook
{
    IIrcComm irc;
    SqliteBack chainey;
    Config config = new Config();

    Random rnd = new Random();
    History<string> history = new History<string>(100);
    
    public string Prefix { get; set; }
    
    public string Name
    {
        get { return "Chainey"; }
    }
    public string Version
    {
        get { return "0.39"; }
    }
    
    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>();
        }
    }


    public void Stop()
    {}
    
    [ImportingConstructor]
    public IrcChainey(IIrcComm ircComm)
    {
        irc = ircComm;
        chainey = new SqliteBack("conf/chainey.sqlite", config.Order);
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }
    
    public void HandleChannelMessage(IIrcMessage e)
    {
        string nick1 = e.MessageArray[0];
        string nick2 = "";
        // Also try with the last character sliced off, since it can be punctuation.
        if (nick1.Length > 1)
            nick2 = nick1.Substring(0, nick1.Length - 1);

        // If directly addressed.
        if (irc.IsMe(nick2) || irc.IsMe(nick1) && e.MessageArray.Length > 1)
            new Thread( () => HandleAddressed(e.Channel, e.Nick, e.MessageArray) ).Start();

        // Only process unaddressed messages if they're worthwhile.
        else if (e.MessageArray.Length >= config.Order)
            new Thread( () => HandleUnaddressed(e.Channel, e.Message) ).Start();
    }


    void HandleAddressed(string channel, string nick, string[] message)
    {
        string chatMessage = string.Join(" ", message, 1, message.Length - 1);
        
        if ( !Unwanted(channel, nick, chatMessage, true) )
        {
            EmitSentence(channel, chatMessage);
            AbsorbSentence(chatMessage);
        }
    }

    void HandleUnaddressed(string channel, string message)
    {
        if ( LearningChannel(channel) &&
            !message.StartsWith(Prefix) &&
            !Unwanted("", "", message, false) )
        {
            AbsorbSentence(message);
        }
    }


    void EmitSentence(string channel, string respondTo)
    {
        string[] selection = GetTopLength(respondTo.Split(' '), config.ResponseTries);
        // Random is _not_ thread safe.
        lock (rnd)
            rnd.Shuffle(selection);

        string[] sentences = chainey.BuildSentences(selection, config.MaxWords);
        foreach (string sen in sentences)
        {
            if (history.Add(sen))
            {
                irc.SendMessage(channel, sen);
                return;
            }
        }

        // We'll get here if none of the seeds gave us a sentence.
        string sentence = chainey.BuildSentence(null, config.MaxWords);
        if (sentence != null)
            irc.SendMessage(channel, sentence);
    }

    void AbsorbSentence(string sentence)
    {
        // Add the whole sentence to the history...
        if (history.Add(sentence))
        {
            string[][] possibleChains = MarkovTools.TokenizeSentence(sentence, config.Order);
            if (possibleChains != null)
            {
                // as well as all the parts/chains.
                string sentencePart;
                foreach (string[] chain in possibleChains)
                {
                    sentencePart = string.Join(" ", chain);
                    history.Add(sentencePart);
                }
                chainey.AddChains(possibleChains);
            }
        }
    }


    // The `direct` parameter is for controlling when to respond to perceived slights. Don't respond if not directly
    // addressed. The `channel` and `nick` parameters are needed to respond to that, they are not used when dealing
    // with an unaddressed message.
    bool Unwanted(string channel, string nick, string message, bool direct)
    {
        foreach (string s in config.BadWords)
        {
            if ( message.Contains(s, StringComparison.OrdinalIgnoreCase) )
            {
                if (direct)
                    irc.SendMessage( channel, string.Format("Bad word detected! I hate you {0}~", nick) );
                return true;
            }
        }

        string[] split = message.Split(' ');

        if ( ChainControl.FoulPlay(split, config.MaxConsecutive, config.MaxTotal) )
        {
            if (direct)
            {
                irc.SendMessage( channel, string.Format("Foul play detected! Stop trying to teach me stupid things, " +
                    "{0}", nick) );
            }
            return true;
        }

        return false;
    }


    // http://www.dotnetperls.com/sort-strings-length
    static string[] GetTopLength(string[] arr, int topCount)
    {
        var sorted = (from s in arr
                      orderby s.Length descending
                      select s).Take(topCount);

        return sorted.ToArray();
    }

    bool LearningChannel(string channel)
    {
        if (!config.Learning)
            return false;
        else
            return config.LearningChannels.Contains(channel);
    }
}


class Config
{
    // Markov Chains of the nth order.
    public int Order { get; set; }

    // If she's learning and from which channels she should be learning.
    public bool Learning { get; set; }
    public HashSet<string> LearningChannels { get; set; }

    // Max number words you want a constructed sentence to have.
    public int MaxWords { get; set; }
    // The number of words it tries as individual seeds before building a random sentence.
    public int ResponseTries { get; set; }

    public List<string> BadWords { get; set; }

    // The max amount of identical words that are allowed to occur in a to-learn sentence, consecutively and in total.
    public int MaxConsecutive { get; set; }
    public int MaxTotal { get; set; }


    public Config()
    {
        Order = 3;

        Learning = true;
        LearningChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"#sankakucomplex"};

        MaxWords = 40;
        ResponseTries = 4;

        BadWords = new List<string>() {" bote"};

        MaxConsecutive = 3;
        MaxTotal = 5;
    }
}