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
    Thread[] consumers;
    readonly Queue<IIrcMessage> MessageQueue = new Queue<IIrcMessage>();
    readonly object _locker = new object();
    
    public string Prefix { get; set; }
    
    public string Name
    {
        get { return "Chainey"; }
    }
    public string Version
    {
        get { return "0.70"; }
    }
    
    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>();
        }
    }


    public void Stop()
    {
        for (int i = 0; i < consumers.Length; i++)
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

        StartConsumers(conf.Threads);

        irc = ircComm;
        irc.AddChannelMessageHandler(Handle);
    }

    public void Handle(IIrcMessage e)
    {
        switch (e.Trigger)
        {
        case null:

            lock (_locker)
            {
                MessageQueue.Enqueue(e);
                Monitor.Pulse(_locker);
            }
            return;
        case "markov":

            var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            msg = msg.Slice(1, 0);

            EmitSentence(e.Channel, msg);
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


    void StartConsumers(int count)
    {
        consumers = new Thread[count];
        for (int i = 0; i < count; i++)
        {
            consumers[i] = new Thread(Consume);
            consumers[i].Start();
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
            HandleUnaddressed(e.Channel, msg);
    }


    void HandleAddressed(string channel, string nick, string[] message)
    {        
        if ( !MarkovTools.FoulPlay(message, conf.MaxConsecutive, conf.MaxTotal) )
        {
            EmitSentence(channel, message);
            AbsorbSentence(message);
        }
        else
            irc.SendMessage(channel, "Foul play detected! Stop trying to teach me stupid things, {0}", nick);
    }

    void HandleUnaddressed(string channel, string[] message)
    {
        if ( RandomRespond(channel) )
            EmitSentence(channel, message);

        if ( Learning(channel) && !MarkovTools.FoulPlay(message, conf.MaxConsecutive, conf.MaxTotal) )
        {
            AbsorbSentence(message);
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


    void AbsorbSentence(string[] sentence)
    {
        if (sentence.Length > 0)
            chainey.Add(sentence);
    }

    void EmitSentence(string channel, string[] respondTo)
    {
        var sw = Stopwatch.StartNew();
        var sentence = chainey.BuildResponse(respondTo);
        sw.Stop();
        Console.WriteLine("-- BuildResponse time: " + sw.Elapsed);

        if (sentence.Content != string.Empty)
        {
            irc.SendMessage(channel, sentence.Content);
            Console.WriteLine("[Chainey] [{0}] {1}", sentence.Rarity, sentence);
        }
    }

}