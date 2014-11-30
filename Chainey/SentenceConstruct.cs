using System;
using System.Collections.Generic;

namespace Chainey
{
    internal class SentenceConstruct
    {
        internal int WordCount
        {
            get { return sentence.Count; }
        }
        
        
        internal string[] Sentence
        {
            get
            {
                var sentenceArr = new string[sentence.Count];
                sentence.CopyTo(sentenceArr, 0);

                return sentenceArr;
            }
        }
        
        
        internal string LatestForwardChain
        {
            get
            {
                var chain = new string[order];
                
                var node = sentence.Last;
                for (int i = order - 1; i >= 0; i--)
                {
                    chain[i] = node.Value;
                    node = node.Previous;
                }
                
                return string.Join(" ", chain);
            }
        }
        
        internal string LatestBackwardChain
        {
            get
            {
                var chain = new string[order];
                
                var node = sentence.First;
                for (int i = 0; i < order; i++)
                {
                    chain[i] = node.Value;
                    node = node.Next;
                }
                
                return string.Join(" ", chain);
            }
        }
        
        LinkedList<string> sentence = new LinkedList<string>();
        
        readonly int order;
        
        
        internal SentenceConstruct(string initialChain)
        {
            string[] split = initialChain.Split(' ');
            foreach (string s in split)
                sentence.AddLast(s);
            
            order = split.Length;
        }
        
        internal void Append(string word)
        {
            sentence.AddLast(word);
        }
        
        internal void Prepend(string word)
        {
            sentence.AddFirst(word);
        }
        
    }


    /* For testing purposes, but it seems my use of LinkedList above is fast enough. SentenceConstruct is not the
     * bottleneck in constructing sentences.
    internal class SentenceConstruct
    {
        int wordCount;
        internal int WordCount
        {
            get { return wordCount; }
        }
        
        
        internal string Sentence
        {
            get
            {
                var words = new List<string>(WordCount);
                
                words.AddRange(backwards);
                words.Reverse();
                
                int start = words.Count - order;
                words.RemoveRange(start, order);
                
                words.AddRange(forwards);
                
                return string.Join(" ", words);
            }
        }
        
        
        internal string LatestForwardChain
        {
            get
            {
                var chain = new string[order];
                
                int start = forwards.Count - order;
                for (int i = 0; i < order; i++)
                {
                    chain[i] = forwards[start + i];
                }
                
                return string.Join(" ", chain);
            }
        }
        
        internal string LatestBackwardChain
        {
            get
            {
                var chain = new string[order];
                
                int start = backwards.Count - 1;
                for (int i = 0; i < order; i++)
                {
                    chain[i] = backwards[start - i];
                }
                
                return string.Join(" ", chain);
            }
        }
        
        List<string> forwards;
        List<string> backwards;
        
        readonly int order;
        
        
        internal SentenceConstruct(string initialChain)
        {
            string[] split = initialChain.Split(' ');
            forwards = new List<string>(split);
            backwards = new List<string>(split);
            backwards.Reverse();
            
            order = split.Length;
            wordCount = split.Length;
        }
        
        internal void Append(string word)
        {
            forwards.Add(word);
            wordCount++;
        }
        
        internal void Prepend(string word)
        {
            backwards.Add(word);
            wordCount++;
        }
        
    } */
}