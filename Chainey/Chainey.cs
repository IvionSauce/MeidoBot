using System;
using System.Diagnostics;
using System.Collections.Generic;
using Chainey;
using IvionSoft;
using MeidoCommon.Parsing;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class IrcChainey : IMeidoHook, IPluginTriggers, IPluginIrcHandlers
{
    public string Name
    {
        get { return "Chainey"; }
    }
    public string Version
    {
        get { return "9999"; } // ENDLESS NINE
    }

    public IEnumerable<Trigger> Triggers { get; private set; }
    public IEnumerable<IIrcHandler> IrcHandlers { get; private set; }


    readonly IIrcComm irc;
    readonly IMeidoComm meido;
    readonly ILog log;

    // Keep Sqlite backend around because we need to dispose of it.
    readonly SqliteBrain backend;
    readonly BrainFrontend chainey;
    readonly Random rnd = new Random();

    readonly Config conf = new Config();

    const string nickPlaceholder = "||NICK||";


    public void Stop()
    {
        backend.Dispose();
    }
    
    [ImportingConstructor]
    public IrcChainey(IIrcComm ircComm, IMeidoComm meidoComm)
    {
        meido = meidoComm;
        log = meido.CreateLogger(this);

        conf.Location = meidoComm.DataPathTo("chainey.sqlite");
        backend = new SqliteBrain(conf.Location, conf.Order);
        chainey = new BrainFrontend(backend);
        chainey.Filter = false;

        irc = ircComm;

        var t = TriggerThreading.Queue;
        Triggers = Trigger.Group(
            
            new Trigger("markov", Markov, t) {
                Help = new TriggerHelp(
                    "<seeds>",
                    "Builds reply with markov chains using the specified seeds.")
            },

            new Trigger(MarkovNick, t, "markov-nick", "nicksay") {
                Help = new TriggerHelp(
                    "<nick> [seeds]",
                    "Builds reply with markov chains based on `seeds`, with the contraint that the words " +
                    "of the reply have been said by `nick` at some point.")
            },

            new Trigger(Remove, t, "markov-remove", "remove") {
                Help = new TriggerHelp(
                    "<sentence>",
                    "Remove sentence and its constituent words from the markov chains database. " +
                    "(Admin only)")
            }
        );

        IrcHandlers = new IIrcHandler[] {
            new IrcHandler<IChannelMsg>(MessageHandler, t)
        };
    }


    // -----------------
    // Chainey triggers.
    // -----------------

    // remove <sentence>
    void Remove(ITriggerMsg e)
    {
        if (meido.AuthLevel(e.Nick) >= 2)
        {
            chainey.Remove(e.ArgString());
            e.Reply("Removed sentence.");
        }
    }

    // markov <seeds>
    void Markov(ITriggerMsg e)
    {
        var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        msg = msg.Slice(1, 0);
        EmitSentence(e.ReturnTo, msg, e.Nick);
    }

    // markov-nick <nick> [seeds]
    void MarkovNick(ITriggerMsg e)
    {
        var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (msg.Length > 1)
        {
            var source = msg[1].ToUpperInvariant();

            List<Sentence> sentences;
            // When we have seeds.
            if (msg.Length > 2)
            {
                msg = msg.Slice(2, 0);
                sentences = chainey.Build(msg, source, false);
            }
            // When we don't have seeds.
            else
                sentences = chainey.BuildRandom(1, source);

            if (sentences.Count > 0)
                SendSentence(e.ReturnTo, sentences[0], e.Nick);
        }
    }


    // ---------------------------------------------
    // Handling of messages (learning and replying).
    // ---------------------------------------------

    void MessageHandler(IChannelMsg e)
    {
        var msg = e.Message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        // Don't process if it's empty or a probable trigger call.
        if (msg.Length == 0 || e.StartsWithTriggerPrefix())
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