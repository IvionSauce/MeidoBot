using System;
using System.Collections.Generic;


class Config
{
    public string Location { get; set; }
    // Markov Chains of the nth order.
    public int Order { get; set; }

    public int Threads { get; set; }

    // If she's learning and from which channels she should be learning.
    public HashSet<string> LearningChannels { get; set; }

    public HashSet<string> RandomResponseChannels { get; set; }
    // One in n (1/n) chance of responding to unaddressed messages.
    public int ResponseChance { get; set; }

    // The max amount of identical words that are allowed to occur in a to-learn sentence, consecutively and in total.
    public int MaxConsecutive { get; set; }
    public int MaxTotal { get; set; }
    
    
    public Config()
    {
        Location = "conf/chainey.sqlite";
        Order = 3;
        Threads = 2;

        LearningChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {"#sankakucomplex", "#SteelGolem", "#blaat"};

        RandomResponseChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        //{"#sankakucomplex", "#SteelGolem", "#blaat"};
        ResponseChance = 300;
        
        MaxConsecutive = 3;
        MaxTotal = 5;
    }
}