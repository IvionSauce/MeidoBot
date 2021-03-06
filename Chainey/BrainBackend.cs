using System;
using System.Collections.Generic;

namespace Chainey
{
    public interface IBrainBackend
    {
        // Add a sentence to the brain. Source can be null or empty to indicate no source.
        void AddSentence(string[] sentence, string source);
        // Remove a sentence.
        void RemoveSentence(string[] sentence);

        // Used to determine which words to use as seeds first (when building a response).
        // Also used to calculate the rarity of a sentence.
        IEnumerable<long> WordCount(IEnumerable<string> words);

        // Fallback for when seed-building fails.
        IEnumerable<Sentence> BuildRandomSentences(string source);
        // Build sentences around given seed.
        IEnumerable<Sentence> BuildSentences(IEnumerable<string> seed, string source);
    }
}