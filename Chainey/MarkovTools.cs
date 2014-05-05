using System;
using System.Collections.Generic;

namespace Chainey
{
    public static class MarkovTools
    {
        public static string[][] TokenizeSentence(string[] sentence, int order)
        {
            if (sentence.Length < order)
                return new string[0][];

            var chainCollection = new List<string[]>();

            string[] chain;

            int last = sentence.Length - order;
            for (int i = 0; i <= last; i++)
            {
                chain = new string[order + 1];
                for (int j = 0; j < chain.Length; j++)
                {
                    if ( (i + j) < sentence.Length )
                        chain[j] = sentence[i + j];
                    else
                        chain[j] = null;
                }
                chainCollection.Add(chain);
            }

            return chainCollection.ToArray();
        }


        public static string[] Filter(string[] sentence)
        {
            string trimmed;
            var cleaned = new List<string>(sentence.Length);
            for (int i = 0; i < sentence.Length; i++)
            {
                if (sentence[i] != null)
                {
                    trimmed = sentence[i].Trim();
                    if (trimmed != string.Empty)
                        cleaned.Add(trimmed);
                }
            }

            return cleaned.ToArray();
        }


        // Will return true if a word occurs consecutively and exceeds the threshold.
        public static bool FoulPlay(string[] words, int consecutiveThreshold, int totalThreshold)
        {
            // Total and consecutive occurrences count.
            int occurrences, consecutive;
            string toCheck;
            // `Length - 1` because we don't need to check the final word, there would be nothing left to match against.
            // This could be more efficient, but I can't be arsed.
            for (int i = 0; i < (words.Length - 1); i++)
            {
                // Set to 1, because whatever word we start on has already occurred once.
                occurrences = consecutive = 1;
                toCheck = words[i];
                
                // We can safely use `i + 1` here because the outer loop always stops 1 short.
                for (int j = (i + 1); j < words.Length; j++)
                {
                    if ( toCheck.Equals(words[j], StringComparison.OrdinalIgnoreCase) )
                    {
                        occurrences++;
                        consecutive++;
                        
                        if (occurrences > totalThreshold || consecutive > consecutiveThreshold)
                            return true;
                    }
                    // Reset consecutive count on non-match.
                    else
                        consecutive = 0;
                }
            }
            
            return false;
        }
    }
}