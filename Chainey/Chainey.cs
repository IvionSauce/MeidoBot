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

    History<string> history = new History<string>(100);

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
        get { return "0.50"; }
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
    public IrcChainey(IIrcComm ircComm)
    {
        chainey = new SqliteBack("conf/chainey.sqlite", config.Order);
        chainey.MaxWords = config.MaxWords;
        StartConsumers(config.Threads);

        irc = ircComm;
        irc.AddChannelMessageHandler(HandleChannelMessage);
    }

    public void HandleChannelMessage(IIrcMessage e)
    {
        // If it's a trigger, ignore it.
        if (e.Trigger == null)
        {
            lock (_locker)
            {
                MessageQueue.Enqueue(e);
                Monitor.Pulse(_locker);
            }
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
        var msg = e.Message.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
        string first = msg[0];
        string last = msg[msg.Length - 1];
        
        // If directly addressed. (nick: message)
        if (first.Contains(irc.Nickname, StringComparison.OrdinalIgnoreCase))
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
        
        else if (LearningChannel(e.Channel))
            HandleUnaddressed(msg);
    }


    void HandleAddressed(string channel, string nick, string[] message)
    {        
        if ( !MarkovTools.FoulPlay(message, config.MaxConsecutive, config.MaxTotal) )
        {
            EmitSentence(channel, message);
            AbsorbSentence(message);
        }
        else
            irc.SendMessage(channel, "Foul play detected! Stop trying to teach me stupid things, {0}", nick);
    }

    void HandleUnaddressed(string[] message)
    {
        if ( !MarkovTools.FoulPlay(message, config.MaxConsecutive, config.MaxTotal) )
            AbsorbSentence(message);
    }


    void EmitSentence(string channel, string[] respondTo)
    {
        // It's okay to change respondTo in place, since it gets copied earlier by Splice.
        chainey.SortByWordCount(respondTo);
        var selection = respondTo.
            Take(config.ResponseTries);

        foreach ( string sen in chainey.BuildSentences(selection) )
        {
            if (history.Add(sen))
            {
                irc.SendMessage(channel, sen);
                return;
            }
        }

        // We'll get here if none of the seeds gave us a sentence.
        string sentence = chainey.BuildRandomSentence();
        if (sentence != null)
            irc.SendMessage(channel, sentence);
    }


    void AbsorbSentence(string[] sentence)
    {
        if (sentence.Length > 0)
        {
            history.Add( string.Join(" ", sentence) );
            chainey.AddSentence(sentence);
        }
    }


    bool LearningChannel(string channel)
    {
        if (config.Learning)
            return config.LearningChannels.Contains(channel);
        else
            return false;
    }
}