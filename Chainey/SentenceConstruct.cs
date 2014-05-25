using System;
using System.Collections.Generic;

namespace Chainey
{
    internal class SentenceConstruct
    {
        internal int WordCount { get; private set; }


        internal string Sentence
        {
            get
            {
                var sen = new List<string>(Backwards);
                sen.RemoveRange(0, order);
                sen.Reverse();
                
                sen.AddRange(Forwards);
                
                return string.Join(" ", sen);
            }
        }


        internal string LatestForwardChain
        {
            get
            {
                var chain = new string[order];
                int start = Forwards.Count - order;
                Forwards.CopyTo(start, chain, 0, order);

                return string.Join(" ", chain);
            }
        }

        internal string LatestBackwardChain
        {
            get
            {
                var chain = new string[order];
                int start = Backwards.Count - order;
                Backwards.CopyTo(start, chain, 0, order);

                return string.Join(" ", chain);
            }
        }
        
        List<string> Forwards;
        List<string> Backwards;
        
        readonly int order;
        
        
        internal SentenceConstruct(string initialChain)
        {
            string[] split = initialChain.Split(' ');
            Forwards = new List<string>(split);
            Backwards = new List<string>(split);
            Backwards.Reverse();

            WordCount = split.Length;
            order = split.Length;
        }
        
        internal void Append(string word)
        {
            Forwards.Add(word);
            WordCount++;
        }
        
        internal void Prepend(string word)
        {
            Backwards.Add(word);
            WordCount++;
        }
    }
}