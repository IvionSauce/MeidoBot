using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using IvionSoft;

namespace Chainey
{
    public class BrainFrontend
    {
        readonly IBrainBackend brain;

        // Our history/memory, keeps track of sentences recently inputted or outputted.
        History<string> history;
        object _historyLock = new object();
        public int Memory
        {
            get
            {
                lock (_historyLock)
                    return history.Length;
            }
            set
            {
                lock (_historyLock)
                    history = new History<string>(value);
            }
        }

        TimeSpan _limit;
        object _limitLock = new object();
        public TimeSpan TimeLimit
        {
            get
            {
                lock (_limitLock)
                    return _limit;
            }
            set
            {
                lock (_limitLock)
                    _limit = value;
            }
        }


        public BrainFrontend(IBrainBackend brain)
        {
            if (brain == null)
                throw new ArgumentNullException("brain");

            this.brain = brain;
            Memory = 100;
            TimeLimit = TimeSpan.FromSeconds(5);
        }


        // ------------------------------------------
        // Methods for adding sentences to the brain.
        // ------------------------------------------

        public void Add(string sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            var split = sentence.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            InternalAdd(split, false);
        }

        public void Add(string[] sentence, bool filter)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            InternalAdd(sentence, filter);
        }

        void InternalAdd(string[] sentence, bool filter)
        {
            // Add to history/memory, we don't want to parrot things we've just learned, like a 3yo.
            lock (_historyLock)
                history.Add( string.Join(" ", sentence) );

            // Make sure the array doesn't contain any null, empty or whitespace items. Also trim before adding.
            // Can be disabled by those who know for sure their array doesn't contain any and want to avoid this cost.
            if (filter)
                brain.AddSentence( MarkovTools.Filter(sentence) );
            else
                brain.AddSentence(sentence);
        }


        // -------------------------------------------
        // Methods for building sentences and replies.
        // -------------------------------------------

        public Sentence BuildRandom()
        {
            // Fall back to an empty string if it fails to build a random sentence.
            string sen = brain.BuildRandomSentence() ?? string.Empty;
            double rarity = brain.SentenceRarity(sen);
            return new Sentence(sen, rarity);
        }


        public Sentence BuildResponse(string message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            return InternalResponse( message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries) );
        }

        public Sentence BuildResponse(string[] message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            return InternalResponse(message);
        }

        Sentence InternalResponse(string[] message)
        {
            // Add to history/memory, so that we don't go straight up repeating people.
            lock (_historyLock)
                history.Add( string.Join(" ", message) );
            
            // Sort so that the rarer words get tried first as seeds.
            string[] sorted = SortByWordCount(message);
            List<Sentence> responses = InternalBuild(sorted);
            // Sort sentences on rarity.
            responses.Sort( (a, b) => a.Rarity.CompareTo(b.Rarity) );
            
            // If we got response sentences, return the most 'rare'.
            if (responses.Count > 0)
            {
                var resp = responses[responses.Count - 1];

                // Debug
                Console.WriteLine("Responses: {0} - High: {1}, Low: {2}",
                                  responses.Count, resp.Rarity, responses[0].Rarity);

                // Add to history/memory, so that we don't go repeating ourselves.
                lock (_historyLock)
                    history.Add(resp.Content);

                return resp;
            }
            // Otherwise return a random sentence.
            else
                return BuildRandom();
        }

        string[] SortByWordCount(string[] words)
        {
            var copy = new string[words.Length];
            Array.Copy(words, copy, words.Length);

            var wordCounts = brain.WordCount(words);
            // Sort the copy, so that we leave the input alone.
            Array.Sort(wordCounts.ToArray(), copy);

            return copy;
        }


        public List<Sentence> Build(IEnumerable<string> seeds)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            return InternalBuild(seeds);
        }

        List<Sentence> InternalBuild(IEnumerable<string> seeds)
        {
            // Get as much sentences from the seeds as time allows.
            var candidates = GetSentences(seeds);
            
            // Remove sentences still in the history/memory. We don't want to repeat what's been recently said to us or
            // said by us.
            lock (_historyLock)
                candidates.RemoveAll( sen => history.Contains(sen.Content) );
            
            return candidates;
        }

        List<Sentence> GetSentences(IEnumerable<string> seeds)
        {
            var sw = Stopwatch.StartNew();

            var sentences = brain.BuildSentences(seeds);
            var rarities = brain.SentenceRarity(sentences);
            
            var pairs = Enumerable.Zip( sentences, rarities,
                                       (s, r) => new Sentence(s, r) );

            // Make sure we have a consistent TimeLimit during the loop execution.
            TimeSpan limit;
            lock (_limitLock)
                limit = TimeLimit;

            var coll = new List<Sentence>();
            // Only bother checking for time if the limit is applicable.
            if (limit > TimeSpan.Zero)
            {
                foreach (var pair in pairs)
                {
                    coll.Add(pair);
                    if (sw.Elapsed > limit)
                        break;
                }
            }
            // Add all Sentence objects when time is not of the essence.
            else
                coll.AddRange(pairs);

            sw.Stop();
            return coll;
        }

    }
}