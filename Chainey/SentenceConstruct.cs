using System;
using System.Collections.Generic;

namespace Chainey
{
    public class SentenceConstruct
    {
        public int WordCount
        {
            get { return Forwards.Count + Backwards.Count - order; }
        }
        
        public string Sentence
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
        
        
        public SentenceConstruct(string initialChain, int order)
        {
            string[] split = initialChain.Split(' ');
            Forwards = new List<string>(split);
            Backwards = new List<string>(split);
            Backwards.Reverse();
            
            this.order = order;
        }
        
        public void Append(string word)
        {
            Forwards.Add(word);
        }
        
        public void Prepend(string word)
        {
            Backwards.Add(word);
        }
        
        
        public string LatestForwardChain()
        {
            if (Forwards.Count < order)
                return string.Empty;
            
            var chain = new string[order];
            int start = Forwards.Count - order;
            Forwards.CopyTo(start, chain, 0, order);
            
            return string.Join(" ", chain);
        }
        
        public string LatestBackwardChain()
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