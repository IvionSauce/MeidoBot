using System;
using System.Collections.Generic;
using IvionSoft;

public class NyaaPatterns : DomainListsReadWriter
{
    public string[] GetPatterns(string channel)
    {
        string chanLow = channel.ToLower();
        
        List<string> patterns;
        string[] value = null;
        
        _rwlock.EnterReadLock();
        if (domainSpecific.TryGetValue(chanLow, out patterns))
            value = patterns.ToArray();
        
        _rwlock.ExitReadLock();
        return value;
    }
    
    // Returns array with the channels that have a pattern matching the passed title.
    public string[] PatternMatch(string title)
    {
        List<string> channels = new List<string>();

        string tmpTitle;
        string[] constituents;
        int countdown;
        
        _rwlock.EnterReadLock();
        // Iterate over the channels.
        foreach (string channel in domainSpecific.Keys)
        {
            // Iterate over the patterns associated with channel.
            foreach (string pattern in domainSpecific[channel])
            {
                // Split each pattern into its constituents. If the title contains one subtract 1 from the countdown.
                // When the countdown reaches 0 it means all constituents were found in the title, so add the channel.
                tmpTitle = title;
                constituents = pattern.Split(' ');
                countdown = constituents.Length;

                foreach (string s in constituents)
                {
                    int startIndex = tmpTitle.IndexOf(s, StringComparison.OrdinalIgnoreCase);

                    if (startIndex >= 0)
                    {
                        // Addendum: if we have a match, remove it from the the tmpTitle, this to ensure that if we have
                        // a pattern with repeated words it only matches when the title indeed contains multiple
                        // instances of that word.
                        tmpTitle = tmpTitle.Remove(startIndex, s.Length);
                        countdown--;
                    }
                }

                if (countdown <= 0)
                {
                    channels.Add(channel);
                    break;
                }
            }
        }
        
        _rwlock.ExitReadLock();
        if (channels.Count > 0)
            return channels.ToArray();
        else
            return null;
    }
}