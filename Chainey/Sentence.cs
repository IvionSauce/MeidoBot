using System;

namespace Chainey
{
    public struct Sentence
    {
        public readonly string Content;
        public readonly double Rarity;
        

        public Sentence(string sentence, double rarity)
        {
            Content = sentence;
            Rarity = rarity;
        }

        public override string ToString()
        {
            return Content;
        }
    }
}