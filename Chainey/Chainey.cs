using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using Chainey;
using IvionSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class IrcChainey : IMeidoHook, IPluginIrcHandlers
{
    public string Name
    {
        get { return "Chainey"; }
    }
    public string Version
    {
        get { return "9999"; } // ENDLESS NINE
    }
    
    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>();
        }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }
    public IEnumerable<IIrcHandler> IrcHandlers { get; private set; }


    readonly IIrcComm irc;
    readonly IMeidoComm meido;
    readonly ILog log;

    readonly BrainFrontend chainey;
    readonly Random rnd = new Random();

    readonly Config conf = new Config();

    // Housekeeping for producer-consumer queue.
    readonly Queue<IChannelMsg> MessageQueue = new Queue<IChannelMsg>();
    readonly object _locker = new object();

    const string nickPlaceholder = "||NICK||";


    public void Stop()
    {
        for (int i = 0; i < conf.Threads; i++)
        {
            lock (_locker)
            {
                MessageQueue.Enqueue(null);
                Monitor.Pulse(_locker);
            }
        }
    }
    
    [ImportingConstructor]
    public IrcChainey(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        meido = meidoComm;
        log = meido.CreateLogger(this);

        conf.Location = meidoComm.DataPathTo("chainey.sqlite");
        chainey = new BrainFrontend( new SqliteBrain(conf.Location, conf.Order) );
        chainey.Filter = false;

        for (int i = 0; i < conf.Threads; i++)
            new Thread(Consume).Start();

        irc = ircComm;

        Triggers = new Trigger[] {
            new Trigger("markov", Markov),
            new Trigger("remove", Remove)
        };

        IrcHandlers = new IIrcHandler[] {
            new IrcHandler<IChannelMsg>(Handler)
        };
    }


    // --------------------------------------------------
    // Boilerplate for the threaded handling of messages.
    // --------------------------------------------------

    void Handler(IChannelMsg e)
    {
        if (e.Trigger == null)
        {
            lock (_locker)
            {
                MessageQueue.Enqueue(e);
                Monitor.Pulse(_locker);
            }
        }
    }

    void Consume()
    {
        IChannelMsg message;
        while (true)
        {
            lock (_locker)
            {
                while (MessageQueue.Count == 0)
                    Monitor.Wait(_locker);

                message = MessageQueue.Dequeue();
            }
            if (message != null)
                ThreadedHandler(message);
            else
                return;
        }
    }


    // -----------------
    // Chainey triggers.
    // -----------------

    public void Remove(IIrcMessage e)
    {
        if (meido.AuthLevel(e.Nick) >= 2)
        {
            var toRemove = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length -1);
            chainey.Remove(toRemove);
            e.Reply("Removed sentence.");
        }
    }

    void Markov(IIrcMessage e)
    {
        var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        string source;
        // markov --nick <nick> [seeds]
        if (msg.Length > 2 && msg[1] == "--nick")
        {
            source = msg[2].ToUpperInvariant();

            List<Sentence> sentences;
            // When we have seeds.
            if (msg.Length > 3)
            {
                msg = msg.Slice(3, 0);
                sentences = chainey.Build(msg, source, false);
            }
            // When we don't have seeds.
            else
                sentences = chainey.BuildRandom(1, source);

            if (sentences.Count > 0)
                SendSentence(e.ReturnTo, sentences[0], e.Nick);
        }
        // markov [seeds]
        else
        {
            msg = msg.Slice(1, 0);
            EmitSentence(e.ReturnTo, msg, e.Nick);
        }
    }


    // ---------------------------------------------
    // Handling of messages (learning and replying).
    // ---------------------------------------------

    void ThreadedHandler(IChannelMsg e)
    {
        var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (msg.Length == 0)
            return;

        string first = msg[0];
        string last = msg[msg.Length - 1];
        
        // If directly addressed. (nick: message || message, nick)
        if (first.StartsWith(irc.Nickname, StringComparison.OrdinalIgnoreCase) ||
            last.Contains(irc.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            HandleAddressed(e.Channel, e.Nick, msg);
        }        
        else
            HandleUnaddressed(e.Channel, e.Nick, msg);
    }


    void HandleAddressed(string channel, string nick, string[] message)
    {        
        if ( !MarkovTools.FoulPlay(message, conf.MaxConsecutive, conf.MaxTotal) )
        {
            EmitSentence(channel, message, nick);
            AbsorbSentence(channel, nick, message);
        }
        else
            irc.SendMessage(channel, "Foul play detected! Stop trying to teach me stupid things, {0}", nick);
    }

    void HandleUnaddressed(string channel, string nick, string[] message)
    {
        if ( RandomRespond(channel) )
            EmitSentence(channel, message, nick);

        if ( Learning(channel) && !MarkovTools.FoulPlay(message, conf.MaxConsecutive, conf.MaxTotal) )
        {
            AbsorbSentence(channel, nick, message);
        }
    }

    bool Learning(string channel)
    {
        return conf.LearningChannels.Contains(channel);
    }

    bool RandomRespond(string channel)
    {
        if (conf.RandomResponseChannels.Contains(channel))
        {
            int chance;
            lock (rnd)
                chance = rnd.Next(conf.ResponseChance);
            
            if (chance == 0)
                return true;
        }
        return false;
    }


    void AbsorbSentence(string channel, string nick, string[] sentence)
    {
        if (Absorb(sentence))
        {
            int nickCount = 0;
            for (int i = 0; i < sentence.Length; i++)
            {
                // Each word is a possible nick.
                string possibleNick = sentence[i].TrimPunctuation();

                // If the word is actually a nick, replace it with the placeholder.
                if ( irc.IsJoined(channel, possibleNick) )
                {
                    sentence[i] = sentence[i].Replace(possibleNick, nickPlaceholder);
                    nickCount++;
                }
                // Abort if someone is trying sabotage.
                else if (possibleNick == nickPlaceholder)
                    return;
            }

            // If a sentence contains more than 2 mentions of a nick, don't add it. It's probably spam anyway.
            if (nickCount < 3)
                chainey.Add(sentence, nick.ToUpperInvariant());
        }
    }

    // Don't absorb a sentence if its length is 0 or when someone is quoting someone verbatim.
    static bool Absorb(string[] sentence)
    {
        if (sentence.Length > 0)
        {
            if (!sentence[0].StartsWith("<", StringComparison.OrdinalIgnoreCase) &&
                !sentence[0].StartsWith("[", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }


    void EmitSentence(string target, string[] respondTo, string fromNick)
    {
        var sw = Stopwatch.StartNew();
        var sentence = chainey.BuildResponse(respondTo);
        sw.Stop();
        log.Verbose("BuildResponse time: {0}", sw.Elapsed);

        SendSentence(target, sentence, fromNick);
    }


    void SendSentence(string target, Sentence sentence, string fromNick)
    {
        if (sentence.Content != string.Empty)
        {
            string senReplaceNicks = sentence.Content.Replace(nickPlaceholder, fromNick);
            
            irc.SendMessage(target, senReplaceNicks);
            log.Message("{0} <> {1}", target, sentence);
        }
    }
}