using System;
using System.Collections.Generic;
using System.Linq;

namespace Chainey
{
    public struct Sentence
    {
        public readonly string Content;
        public readonly double Rarity;
        

        public Sentence(string sentence, double rarity)
        {
            Content = sentence;
            Rarity = rarity;
        }

        public Sentence(IEnumerable<string> sentenceWords, double rarity)
        {
            if (sentenceWords == null)
                throw new ArgumentNullException("words");

            Content = string.Join(" ", sentenceWords);
            Rarity = rarity;
        }

        public Sentence(IEnumerable<string> sentenceWords, IEnumerable<long> wordCounts)
        {
            if (sentenceWords == null)
                throw new ArgumentNullException("sentenceWords");
            else if (wordCounts == null)
                throw new ArgumentNullException("wordCounts");

            Content = string.Join(" ", sentenceWords);
            Rarity = CalculateRarity(wordCounts);
        }

        internal Sentence(ArraySegment<string> sentenceWords, IEnumerable<long> wordCounts)
        {
            Content = string.Join(" ", sentenceWords.Array, sentenceWords.Offset, sentenceWords.Count);
            Rarity = CalculateRarity(wordCounts);
        }


        // The closer to 0, the less rare the sentence is. If the sentence contains only words we've never seen before
        // the rarity will be `Infinity`.
        // Will return `-Infinity` if the sentence has no words.
        // If sorted order will be: NaN, -Infinity, [...], Infinity
        static double CalculateRarity(IEnumerable<long> wordCounts)
        {
            int len = wordCounts.Count();
            // Sum word counts in ulong for extra headroom.
            ulong sum = 0;
            foreach (long count in wordCounts)
            {
                // Skip negative word counts.
                if (count >= 0)
                    sum += (ulong)count;
                else
                    len--;
            }

            if (len > 0)
                return (double)len / sum;
            else
                return double.NegativeInfinity;
        }


        public override string ToString()
        {
            return Content;
        }
    }
}