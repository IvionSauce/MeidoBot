// using System.Threading;
using System;
using System.Collections.Generic;
using IvionSoft;


public class ControlList : DomainListsReader
{
    public bool IsInList(string url, string channel, string nick)
    {
        _rwlock.EnterReadLock();
        
        // Check the global blacklist.
        foreach (string s in globalList)
        {
            if (url.Contains(s, StringComparison.OrdinalIgnoreCase))
            {
                _rwlock.ExitReadLock();
                return true;
            }
        }
        
        // Check for a channel specific blacklist, since only a minority will have one it will cause the
        // foreach loop to be skipped. (Acting on the assumption that TryGetValue is efficient)
        List<string> domainList;
        if (domainSpecific.TryGetValue(channel.ToLower(), out domainList) ||
            domainSpecific.TryGetValue(nick.ToLower(), out domainList) )
        {
            foreach (string s in domainList)
            {
                if (url.Contains(s, StringComparison.OrdinalIgnoreCase))
                {
                    _rwlock.ExitReadLock();
                    return true;
                }
            }
        }
        
        _rwlock.ExitReadLock();
        // If neither return a hit, return false - since it's in neither of the blacklists.
        return false;
    }
}


public class NickDisable
{
    Dictionary<string, HashSet<string>> disabledNicks = new Dictionary<string, HashSet<string>>();
    object _locker = new object();

    public bool IsNickDisabled(string nick, string channel)
    {
        string chanLow = channel.ToLower();

        lock (_locker)
        {
            HashSet<string> nickHashes;
            if (disabledNicks.TryGetValue(chanLow, out nickHashes))
                return nickHashes.Contains(nick);
            else
                return false;
        }
    }

    public bool Add(string nick, string channel)
    {
        string chanLow = channel.ToLower();

        lock (_locker)
        {
            HashSet<string> nickHashes;
            if (disabledNicks.TryGetValue(chanLow, out nickHashes))
                return nickHashes.Add(nick);
            else
            {
                disabledNicks.Add(chanLow, new HashSet<string>());
                return disabledNicks[chanLow].Add(nick);
            }
        }
    }

    public bool Remove(string nick, string channel)
    {
        string chanLow = channel.ToLower();

        lock (_locker)
        {
            HashSet<string> nickHashes;
            if (disabledNicks.TryGetValue(chanLow, out nickHashes))
                return nickHashes.Remove(nick);
            else
                return false;
        }
    }
}