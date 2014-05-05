using System;
using System.Collections.Generic;


class Config
{
    // Markov Chains of the nth order.
    public int Order { get; set; }
    
    // If she's learning and from which channels she should be learning.
    public bool Learning { get; set; }
    public HashSet<string> LearningChannels { get; set; }
    
    public int Threads { get; set; }

    // The number of words it tries as individual seeds before building a random sentence.
    public int ResponseTries { get; set; }
    
    // The max amount of identical words that are allowed to occur in a to-learn sentence, consecutively and in total.
    public int MaxConsecutive { get; set; }
    public int MaxTotal { get; set; }
    
    
    public Config()
    {
        Order = 2;
        
        Learning = true;
        LearningChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {"#sankakucomplex", "#SteelGolem"};
        
        Threads = 3;

        ResponseTries = 4;
        
        MaxConsecutive = 3;
        MaxTotal = 5;
    }
}