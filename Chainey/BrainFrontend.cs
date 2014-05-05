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
        volatile History<string> history;

        public int Memory
        {
            get { return history.Length; }
            set
            {
                if (value > 0)
                    history = new History<string>(value);
                else
                    history = null;
            }
        }

        TimeSpan _limit;
        object _locker = new object();
        public TimeSpan TimeLimit
        {
            get
            {
                lock (_locker)
                    return _limit;
            }
            set
            {
                lock (_locker)
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
            if (history != null)
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

        // Input message will be modified, due to sort.
        public Sentence BuildResponse(string[] message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            return InternalResponse(message);
        }

        Sentence InternalResponse(string[] message)
        {
            // Add to history/memory, so that we don't go straight up repeating people.
            if (history != null)
                history.Add( string.Join(" ", message) );
            
            // Sort so that the rarer words get tried first as seeds.
            SortByWordCount(message);
            var responses = Build(message);
            // Sort sentences on rarity.
            responses.Sort( (a, b) => a.Rarity.CompareTo(b.Rarity) );
            
            // If we got response sentences, return the most 'rare'.
            if (responses.Count > 0)
            {
                var resp = responses[responses.Count - 1];
                if (history != null)
                    history.Add(resp.Content);

                return resp;
            }
            // Otherwise return a random sentence.
            else
                return BuildRandom();
        }

        void SortByWordCount(string[] words)
        {
            var wordCounts = brain.WordCount(words);
            Array.Sort(wordCounts.ToArray(), words);
        }


        public List<Sentence> Build(IEnumerable<string> seeds)
        {
            // Get as much sentences from the seeds as time allows.
            var candidates = GetSentences(seeds);

            // Remove sentences still in the history/memory.
            if (history != null)
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
            lock (_locker)
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
            // Otherwise simply add all Sentence objects.
            else
                coll.AddRange(pairs);

            sw.Stop();
            return coll;
        }

    }
}