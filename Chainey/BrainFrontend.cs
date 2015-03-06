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
            TimeLimit = TimeSpan.FromSeconds(2);
        }


        // ------------------------------------------
        // Methods for adding sentences to the brain.
        // ------------------------------------------

        public void Add(string sentence, string source)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            var split = sentence.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            InternalAdd(split, source);
        }

        public void Add(string[] sentence, string source)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            if (Filter)
                InternalAdd(MarkovTools.Filter(sentence), source);
            else
                InternalAdd(sentence, source);
        }

        void InternalAdd(string[] sentence, string source)
        {
            // Add to history/memory, we don't want to parrot things we've just learned, like a 3yo.
            lock (_historyLock)
                history.Add( string.Join(" ", sentence) );

            brain.AddSentence(sentence, source);
        }


        // ----------------------------------------------
        // Methods for deleting sentences from the brain.
        // ----------------------------------------------

        public void Remove(string sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            var split = sentence.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            brain.RemoveSentence(split);
        }

        public void Remove(string[] sentence)
        {
            if (sentence == null)
                throw new ArgumentNullException("sentence");

            if (Filter)
                brain.RemoveSentence( MarkovTools.Filter(sentence) );
            else
                brain.RemoveSentence(sentence);
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

            List<string> seeds = brain.GetSeeds(message, 2);
            List<Sentence> responses = InternalBuild(seeds, null, true);

            if (responses.Count > 0)
            {
                Sentence resp = Select(responses, seeds);
                // Add to history/memory, so that we don't go repeating ourselves.
                lock (_historyLock)
                    history.Add(resp.Content);

                return resp;
            }
            else
                return new Sentence(string.Empty, double.NegativeInfinity);
        }


        Sentence Select(List<Sentence> responses, List<string> seeds)
        {
            List<Sentence> candidates;
            if (seeds.Count > 1)
            {
                candidates = MatchSeeds(responses, seeds);

                if (candidates.Count == 0)
                    candidates = responses;
            }
            else
                candidates = responses;

            // Debug
            Console.WriteLine("--- Responses: {0} | Candidates: {1}", responses.Count, candidates.Count);

            return SentenceSelector.Select(candidates);
        }

        List<Sentence> MatchSeeds(List<Sentence> candidates, List<string> seeds)
        {
            var matches = new List<Sentence>();
            // Prepend space so as to not match irrelevant words. Do allow other characters to follow it (plural,
            // punctuation, conjugation).
            string seed = " " + seeds[0];
            string extraSeed = " " + seeds[1];

            foreach (var sen in candidates)
            {
                if (sen.Content.Contains(extraSeed, StringComparison.OrdinalIgnoreCase) &&
                    sen.Content.Contains(seed, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(sen);
                }
            }

            return matches;
        }


        // -------------------------------
        // Methods for building sentences.
        // -------------------------------


        public List<Sentence> BuildRandom(int count, string source)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException("count", "Cannot be 0 or negative.");

            var coll = brain.BuildRandomSentences(source).Take(count);

            return coll.ToList();
        }


        public List<Sentence> Build(IEnumerable<string> seeds, string source)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            return InternalBuild(seeds, source, false);
        }

        public List<Sentence> Build(IEnumerable<string> seeds, string source, bool memoryFilter)
        {
            if (seeds == null)
                throw new ArgumentNullException("seeds");

            return InternalBuild(seeds, source, memoryFilter);
        }

        List<Sentence> InternalBuild(IEnumerable<string> seeds, string source, bool memoryFilter)
        {
            var sentences = brain.BuildSentences(seeds, source);
            var responses = GetSentences(sentences);

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
        List<Sentence> GetSentences(IEnumerable<Sentence> sentences)
        {
            // Make sure we have a consistent TimeLimit during the loop execution.
            var limit = TimeLimit.TotalMilliseconds;

            var coll = new List<Sentence>();
            // Only bother checking for time if the limit is applicable.
            if (limit > 0)
            {
                const int Interval = 256;
                int loopCount = 1;

                var startTicks = (uint)Environment.TickCount;
                foreach (var sen in sentences)
                {
                    coll.Add(sen);
                    if (loopCount == Interval)
                    {
                        if ( ((uint)Environment.TickCount - startTicks) > limit )
                            break;
                        else
                            loopCount = 0;
                    }

                    loopCount++;
                }
            }
            // Add all Sentence objects when time is not of the essence.
            else
                coll.AddRange(sentences);

            return coll;
        }

    }
}