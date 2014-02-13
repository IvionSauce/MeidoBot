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
        
        _rwlock.EnterReadLock();
        // Iterate over the channels.
        foreach (string channel in domainSpecific.Keys)
        {
            // Iterate over the patterns associated with channel.
            foreach (string pattern in domainSpecific[channel])
            {
                // Split each pattern into its constituents. If the title contains it subtract 1 from the countdown.
                // When the countdown reaches 0 it means all constituents were found in the title, so add the channel.
                string[] split = pattern.Split(' ');
                int countdown = split.Length;

                foreach (string s in split)
                    if ( title.Contains(s, StringComparison.OrdinalIgnoreCase) )
                        countdown--;

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


static class ExtensionMethods
{
    public static bool Contains(this string source, string value, StringComparison comp)
    {
        return source.IndexOf(value, comp) >= 0;
    }
}