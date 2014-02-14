// using System.Threading;
using System;
using System.Collections.Generic;
using IvionSoft;


public class Whitelist : ControlList
{
    public bool? IsInList(string url, string channel, string nick)
    {
        /* bool inGlobal = IsInGlobalList(url);
        if (inGlobal)
            return true; */
        
        bool? inChannelList = IsInDomainList(url, channel);
        bool? inNickList = IsInDomainList(url, nick);

        if (inChannelList == true || inNickList == true)
            return true;
        else if (inChannelList == false || inNickList == false)
            return false;
        else
            return null;
    }
}


public class Blacklist : ControlList
{
    public bool IsInList(string url, string channel, string nick)
    {
        bool inGlobal = IsInGlobalList(url);
        if (inGlobal)
            return true;

        bool? inChannelList = IsInDomainList(url, channel);
        bool? inNickList = IsInDomainList(url, nick);

        if (inChannelList == true || inNickList == true)
            return true;
        else
            return false;
    }
}


public class ControlList : DomainListsReader
{
    public bool? IsInDomainList(string url, string domain)
    {
        _rwlock.EnterReadLock();
        List<string> domainList;
        if (domainSpecific.TryGetValue(domain.ToLower(), out domainList))
        {
            foreach (string s in domainList)
            {
                if (url.Contains(s, StringComparison.OrdinalIgnoreCase))
                {
                    _rwlock.ExitReadLock();
                    return true;
                }
            }
            _rwlock.ExitReadLock();
            // Return false if it does have an entry, but the URL isn't in the list.
            return false;
        }

        _rwlock.ExitReadLock();
        // Return null if it doesn't even have an entry.
        return null;
    }

    public bool IsInGlobalList(string url)
    {
        _rwlock.EnterReadLock();
        foreach (string s in globalList)
        {
            if (url.Contains(s, StringComparison.OrdinalIgnoreCase))
            {
                _rwlock.ExitReadLock();
                return true;
            }
        }
        _rwlock.ExitReadLock();

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