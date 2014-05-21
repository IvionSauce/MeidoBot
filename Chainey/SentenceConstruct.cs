using System;
using System.Collections.Generic;

namespace Chainey
{
    internal class SentenceConstruct
    {
        internal int WordCount
        {
            get { return Forwards.Count + Backwards.Count - order; }
        }
        
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
        
        List<string> Forwards;
        List<string> Backwards;
        
        readonly int order;
        
        
        internal SentenceConstruct(string initialChain, int order)
        {
            string[] split = initialChain.Split(' ');
            Forwards = new List<string>(split);
            Backwards = new List<string>(split);
            Backwards.Reverse();
            
            this.order = order;
        }
        
        internal void Append(string word)
        {
            Forwards.Add(word);
        }
        
        internal void Prepend(string word)
        {
            Backwards.Add(word);
        }
        
        
        internal string LatestForwardChain()
        {
            if (Forwards.Count < order)
                return string.Empty;
            
            var chain = new string[order];
            int start = Forwards.Count - order;
            Forwards.CopyTo(start, chain, 0, order);
            
            return string.Join(" ", chain);
        }
        
        internal string LatestBackwardChain()
        {
            if (Backwards.Count < order)
                return string.Empty;
            
            var chain = new string[order];
            int start = Backwards.Count - order;
            Backwards.CopyTo(start, chain, 0, order);
            
            return string.Join(" ", chain);
        }
    }
}