using System;
using System.Linq;
using System.Collections.Generic;
using IvionSoft;


namespace Chainey
{
    public static class SeedSelector
    {
        // Sort and return the least common words (with `count` as max seed count). Also trim punctuation.
        public static List<string> GetSeeds(this IBrainBackend brain, IList<string> words, int count)
        {
            // Make copy for sorting.
            var copy = new string[words.Count];
            words.CopyTo(copy, 0);

            var wordCounts = brain.WordCount(copy).ToArray();
            Array.Sort(wordCounts, copy);

            var seeds = new List<string>(count);
            for (int i = 0; i < copy.Length && seeds.Count < count; i++)
            {
                if (wordCounts[i] > 0)
                {
                    string trimmed = copy[i].TrimPunctuation();
                    if (trimmed.Length >= 1)
                        seeds.Add(trimmed);
                }
            }

            return seeds;
        }

    }


    public static class SentenceSelector
    {
        readonly static Random rnd = new Random();


        public static Sentence Select(List<Sentence> sentences)
        {
            if (sentences == null)
                throw new ArgumentNullException("sentences");
            else if (sentences.Count == 0)
                throw new ArgumentOutOfRangeException("sentences", "Collection must be non-empty.");

            sentences.SortByRarity();
            int half = (sentences.Count - 1) / 2;
            int third = (sentences.Count - 1)  / 3;
            int twoThirds = third * 2;

            // We want either the sentences between 1/2 and 2/3 or 1/3 and 2/3, we will discard the top 1/3 sentences.
            // Since `sentences` is sorted from least to most rare those correspond to the rarest sentences. We do this
            // because the rarest sentences probably contain typo's, that's why they're rare.

            int index;

            const int thresh = 7;
            // If enough options, return random sentence between 1/2 and 2/3 rare.
            if ( (twoThirds - half) >= thresh )
            {
                lock (rnd)
                    index = rnd.Next(half, twoThirds + 1);
            }
            // If enough options, return random sentence between 1/3 and 2/3 rare.
            else if ( (twoThirds - third) >= thresh )
            {
                lock (rnd)
                    index = rnd.Next(third, twoThirds + 1);
            }
            // If not enough options, return random sentence.
            else
            {
                lock (rnd)
                    index = rnd.Next(sentences.Count);
            }

            return sentences[index];
        }


        public static Sentence SelectRandom(IList<Sentence> sentences)
        {
            if (sentences == null)
                throw new ArgumentNullException("sentences");
            else if (sentences.Count == 0)
                throw new ArgumentOutOfRangeException("sentences", "Collection must be non-empty.");

            lock (rnd)
            {
                int index = rnd.Next(sentences.Count);
                return sentences[index];
            }
        }


        public static Sentence MostRare(List<Sentence> sentences)
        {
            if (sentences == null)
                throw new ArgumentNullException("sentences");
            else if (sentences.Count == 0)
                throw new ArgumentOutOfRangeException("sentences", "Collection must be non-empty.");

            sentences.SortByRarity();
            return sentences[sentences.Count - 1];
        }

        public static Sentence LeastRare(List<Sentence> sentences)
        {
            if (sentences == null)
                throw new ArgumentNullException("sentences");
            else if (sentences.Count == 0)
                throw new ArgumentOutOfRangeException("sentences", "Collection must be non-empty.");

            sentences.SortByRarity();
            return sentences[0];
        }


        public static void SortByRarity(this List<Sentence> sentences)
        {
            if (sentences == null)
                throw new ArgumentNullException("sentences");

            sentences.Sort( (a, b) => a.Rarity.CompareTo(b.Rarity) );
        }
    }
}