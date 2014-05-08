using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using IvionSoft;

namespace Chainey
{
    public class BrainFrontend
    {
        // Make sure the array doesn't contain any null, empty or whitespace items. Also trim before adding.
        // Can be disabled by those who know for sure their array doesn't contain any and want to avoid this cost.
        volatile bool _filter;
        public bool Filter
        {
            get { return _filter; }
            set { _filter = value; }
        }

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

        readonly IBrainBackend brain;


        public BrainFrontend(IBrainBackend brain)
        {
            if (brain == null)
                throw new ArgumentNullException("brain");

            this.brain = brain;
            Filter = true;
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
            InternalAdd(split);
        }

        public void Add(string[] sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            if (Filter)
                InternalAdd( MarkovTools.Filter(sentence) );
            else
                InternalAdd(sentence);
        }

        void InternalAdd(string[] sentence)
        {
            // Add to history/memory, we don't want to parrot things we've just learned, like a 3yo.
            lock (_historyLock)
                history.Add( string.Join(" ", sentence) );

            brain.AddSentence(sentence);
        }


        // -----------------------------
        // Methods for building replies.
        // -----------------------------


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

            if (Filter)
                return InternalResponse( MarkovTools.Filter(message) );
            else
                return InternalResponse(message);
        }

        Sentence InternalResponse(string[] message)
        {
            // Add to history/memory, so that we don't go straight up repeating people.
            lock (_historyLock)
                history.Add( string.Join(" ", message) );
            
            // Sort so that the rarer words get tried first as seeds.
            string[] sorted = SortByWordCount(message);
            List<Sentence> responses = InternalBuild(sorted, true);

            // If we got response sentences, return the most 'rare'.
            if (responses.Count > 0)
            {
                Sentence resp = Select(responses);
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


        Sentence Select(List<Sentence> candidates)
        {
            // Sort sentences on rarity.
            candidates.Sort( (a, b) => a.Rarity.CompareTo(b.Rarity) );
            // Rarest sentence.
            var response = candidates[candidates.Count - 1];

            // Debug
            Console.WriteLine("---\nDebug -- Responses: {0} - High: {1}, Low: {2}",
                              candidates.Count, response.Rarity, candidates[0].Rarity);

            return response;
        }

        // -------------------------------
        // Methods for building sentences.
        // -------------------------------


        public Sentence BuildRandom()
        {
            // Fall back to an empty string if it fails to build a random sentence.
            string sen = brain.BuildRandomSentence() ?? string.Empty;
            double rarity = SentenceRarity(sen);
            return new Sentence(sen, rarity);
        }


        public List<Sentence> Build(IEnumerable<string> seeds)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            return InternalBuild(seeds, false);
        }

        public List<Sentence> Build(IEnumerable<string> seeds, bool memoryFilter)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            return InternalBuild(seeds, memoryFilter);
        }

        List<Sentence> InternalBuild(IEnumerable<string> seeds, bool memoryFilter)
        {
            var responses = GetSentences(seeds);
            if (memoryFilter)
            {
                // Remove sentences still in the history/memory. We don't want to repeat what's been recently said to us
                // or said by us.
                lock (_historyLock)
                    responses.RemoveAll( sen => history.Contains(sen.Content) );
            }
            
            return responses;
        }


        // Get as much sentences from the seeds as time allows.
        List<Sentence> GetSentences(IEnumerable<string> seeds)
        {
            var sw = Stopwatch.StartNew();

            var sentences = brain.BuildSentences(seeds);
            // Sentence and rarity pairs, united in a Sentence struct.
            var pairs = SentenceAndRarity(sentences);

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


        // ----------------------------------------
        // Methods for calculating sentence rarity.
        // ----------------------------------------


        IEnumerable<Sentence> SentenceAndRarity(IEnumerable<string> sentences)
        {
            foreach (string sen in sentences)
                yield return new Sentence(sen, SentenceRarity(sen));
        }

        double SentenceRarity(string sentence)
        {            
            var split = sentence.Split();
            var counts = brain.WordCount(split);

            return CalculateRarity(counts);
        }

        // The closer to 0, the less rare the sentence is. If the sentence contains only words we've never seen before
        // the rarity will be `Infinity`.
        // Will return `-Infinity` if the sentence has no words.
        // If sorted order will be: NaN, -Infinity, [...], Infinity
        double CalculateRarity(IEnumerable<long> counts)
        {
            int len = 0;
            // Sum word counts in ulong for extra headroom.
            ulong sum = 0;
            foreach (long count in counts)
            {
                // Skip negative word counts.
                if (count >= 0)
                {
                    sum += (ulong)count;
                    len++;
                }
            }

            if (len > 0)
                return (double)len / sum;
            else
                return double.NegativeInfinity;
        }

    }
}