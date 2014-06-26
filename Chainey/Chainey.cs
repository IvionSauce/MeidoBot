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
public class IrcChainey : IMeidoHook
{
    readonly IIrcComm irc;
    readonly IMeidoComm meido;

    readonly BrainFrontend chainey;
    readonly Random rnd = new Random();

    readonly Config conf = new Config();

    // Housekeeping for producer-consumer queue.
    readonly Queue<IIrcMessage> MessageQueue = new Queue<IIrcMessage>();
    readonly object _locker = new object();
    
    public string Prefix { get; set; }
    
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

        chainey = new BrainFrontend( new SqliteBrain(conf.Location, conf.Order) );
        chainey.Filter = false;

        for (int i = 0; i < conf.Threads; i++)
            new Thread(Consume).Start();

        irc = ircComm;
        irc.AddChannelMessageHandler(Handle);
        irc.AddTriggerHandler(HandleTrigger);
    }

    public void Handle(IIrcMessage e)
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

    public void HandleTrigger(IIrcMessage e)
    {
        switch (e.Trigger)
        {
        case "markov":
            var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            msg = msg.Slice(1, 0);
            
            EmitSentence(e.ReturnTo, msg, e.Nick);
            return;
        case "remove":
            if (meido.AuthLevel(e.Nick) >= 9)
            {
                var toRemove = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length -1);
                chainey.Remove(toRemove);
                e.Reply("Removed sentence.");
            }
            return;
        }
    }


    void Consume()
    {
        IIrcMessage message;
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


    void ThreadedHandler(IIrcMessage e)
    {
        var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        string first = msg[0];
        string last = msg[msg.Length - 1];
        
        // If directly addressed. (nick: message)
        if (first.StartsWith(irc.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            msg = msg.Slice(1, 0);
            HandleAddressed(e.Channel, e.Nick, msg);
        }
        // If directly addressed. (message, nick)
        else if (last.Contains(irc.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            msg = msg.Slice(0, -1);
            
            // Remove comma if message now ends with it due to the removal of the nick.
            last = msg[msg.Length - 1];
            if (last.EndsWith(",", StringComparison.OrdinalIgnoreCase))
                msg[msg.Length - 1] = last.Substring(0, last.Length - 1);
            
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
                chainey.Add(sentence, nick);
        }
    }

    // Don't absorb a sentence if its length is 0 or when someone is quoting someone verbatim.
    bool Absorb(string[] sentence)
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
        Console.WriteLine("-- BuildResponse time: " + sw.Elapsed);

        if (sentence.Content != string.Empty)
        {
            string senReplaceNicks = sentence.Content.Replace(nickPlaceholder, fromNick);

            irc.SendMessage(target, senReplaceNicks);
            Console.WriteLine("[Chainey] [{0}] {1}", sentence.Rarity, sentence);
        }
    }

}