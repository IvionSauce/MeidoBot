using System;
using System.Collections.Generic;

namespace Chainey
{
    public interface IBrainBackend
    {
        // Add a sentence to the brain.
        void AddSentence(string[] sentence);

        // Used to determine which words to use as seeds first (when building a response).
        // Also used to calculate the rarity of a sentence.
        IEnumerable<long> WordCount(IEnumerable<string> words);

        // Used to determine which sentence to respond with.
        //double SentenceRarity(string sentence);
        //IEnumerable<double> SentenceRarity(IEnumerable<string> sentences);

        // Fallback for when seed-building fails.
        string BuildRandomSentence();
        // Build sentences around given seeds.
        IEnumerable<string> BuildSentences(IEnumerable<string> seeds);
    }
}