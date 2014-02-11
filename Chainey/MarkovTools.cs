using System;
using System.Collections.Generic;

namespace Chainey
{
    static public class MarkovTools
    {
        static public string[][] TokenizeSentence(string sentence, int order)
        {
            string[] split = sentence.Split(' ');
            return TokenizeSentence(split, order);
        }

        static public string[][] TokenizeSentence(string[] sentence, int order)
        {
            if ( sentence.Length < order )
                return null;

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

        static public string GetLatestChain(IList<string> sentence, int order)
        {
            var chain = new string[order];
            
            int j = (sentence.Count - order);
            for (int i = 0; i < order; i++)
            {
                chain[i] = sentence[j];
                j++;
            }
            
            return string.Join(" ", chain);
        }
    }


    static public class ChainControl
    {
        /* static public HashSet<string> IgnoreChatWords { get; set; }
        
        
        static ChainControl()
        {
            IgnoreChatWords = new HashSet<string>() {"a", "an", "and", "the", "that", "of", "to", "in", "it", "is",
                "was", "i", "I", "for", "on"};
        }

        static public string[] RemoveIgnoredWords(string[] sentence)
        {
            var newSentence = new List<string>();
            foreach (string s in sentence)
            {
                if (!IgnoreChatWords.Contains(s))
                    newSentence.Add(s);
            }

            return newSentence.ToArray();
        } */

        // Will return true if a word occurs consecutively and exceeds the threshold.
        static public bool FoulPlay(string[] words, int consecutiveThreshold, int totalThreshold)
        {
            // Total and consecutive occurrences count.
            int occurrences, consecutive;
            string toCheck, word;
            // `Length - 1` because we don't need to check the final word, there would be nothing left to match against.
            for (int i = 0; i < (words.Length - 1); i++)
            {
                occurrences = consecutive = 0;
                toCheck = words[i].ToLower();

                // We can safely use `i + 1` here because the outer loop always stops 1 short.
                for (int j = (i + 1); j < words.Length; j++)
                {
                    word = words[j];
                    // Check for null, since the last word can be null (end of the chain).
                    if (word != null && word.ToLower() == toCheck)
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