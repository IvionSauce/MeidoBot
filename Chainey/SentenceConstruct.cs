using System;
using System.Collections.Generic;

namespace Chainey
{
    internal class SentenceConstruct
    {
        public int WordCount { get; private set; }

        public bool Continue
        {
            get { return WordCount < maxWords; }
        }

        public Func<string> LatestChain { get; private set; }
        public Action<string> ModeAdd { get; private set; }

        readonly string[] sentence;
        int senStart, senEnd;

        readonly int order;
        readonly int maxWords;


        public SentenceConstruct(int order, int maxWords)
        {
            this.order = order;
            this.maxWords = maxWords;

            int arrSize = (maxWords * 2) - order;
            sentence = new string[arrSize];
        }


        // -------------------------------------------------------
        // Methods for setting the initial chain and adding words.
        // -------------------------------------------------------

        public void Set(string initialChain)
        {
            string[] split = initialChain.Split(' ');
            WordCount = split.Length;

            int initialChainPos = maxWords - order;
            senStart = initialChainPos;
            for (int i = 0; i < split.Length; i++, initialChainPos++)
            {
                sentence[initialChainPos] = split[i];
            }
            senEnd = initialChainPos - 1;
        }

        public void Append(string word)
        {
            // Points to the current head(/end) of the sentence, increment to append.
            senEnd++;
            sentence[senEnd] = word;
            WordCount++;
        }

        public void Prepend(string word)
        {
            // Points to the current tail(/beginning) of the sentence, decrement to prepend.
            senStart--;
            sentence[senStart] = word;
            WordCount++;
        }

        // ---------------------------------------------------------------------------------
        // Methods for getting chains while building and for getting the sentence when done.
        // ---------------------------------------------------------------------------------

        public string ForwardChain()
        {
            // Get last n (order) words.
            int forwardStart = (senEnd + 1) - order;
            var chain = string.Join(" ", sentence, forwardStart, order);
            return chain;
        }

        public string BackwardChain()
        {
            // Get first n (order) words.
            var chain = string.Join(" ", sentence, senStart, order);
            return chain;
        }

        public string[] ToArray()
        {
            var sentence = new string[WordCount];
            Array.Copy(this.sentence, senStart, sentence, 0, WordCount);

            return sentence;
        }

        public ArraySegment<string> CurrentSegment()
        {
            return new ArraySegment<string>(sentence, senStart, WordCount);
        }

        // ---------------------
        // Mode setting methods.
        // ---------------------

        public void AppendMode()
        {
            ModeAdd = Append;
            LatestChain = ForwardChain;
        }

        public void PrependMode()
        {
            ModeAdd = Prepend;
            LatestChain = BackwardChain;
        }
    }
}