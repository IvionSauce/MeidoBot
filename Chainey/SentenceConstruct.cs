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


        internal string Sentence
        {
            get { return string.Join(" ", sentence); }
        }


        internal string LatestForwardChain
        {
            get
            {
                var chain = new string[order];

                var node = sentence.Last;
                for (int i = 0; i < order; i++)
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
}