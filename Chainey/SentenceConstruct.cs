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


        internal string LatestForwardChain
        {
            get
            {
                int start = Forwards.Count - order;            
                return string.Join(" ", Forwards, start, order);
            }
        }

        internal string LatestBackwardChain
        {
            get
            {
                int start = Backwards.Count - order;            
                return string.Join(" ", Backwards, start, order);
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
    }
}