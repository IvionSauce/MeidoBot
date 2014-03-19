using System;
using System.Collections.Generic;
using IvionSoft;


public class Whitelist : ControlList
{
    public bool? IsInList(string url, string channel, string nick)
    {        
        bool? inChannelList = IsInDomainList(channel, url);
        bool? inNickList = IsInDomainList(nick, url);

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

        bool? inChannelList = IsInDomainList(channel, url);
        bool? inNickList = IsInDomainList(nick, url);

        if (inChannelList == true || inNickList == true)
            return true;
        else
            return false;
    }
}


// Common methods for both Black- and Whitelist.
// Also wrap access to DomainLists to provide a workable (not exploding) interface even if there's nothing loaded.
public class ControlList
{
    DomainLists domLists = null;
    string path = null;
    
    
    public bool IsInGlobalList(string url)
    {
        if (domLists != null)
            return domLists.IsInGlobalList(url);
        else
            return false;
    }

    public bool? IsInDomainList(string domain, string url)
    {
        if (domLists != null)
            return domLists.IsInDomainList(domain, url);
        else
            return null;
    }

    public void LoadFromFile(string path)
    {
        if (this.path != null)
            this.path = null;

        var tmpDomLists = new DomainLists(path);
        domLists = tmpDomLists;
        this.path = path;
    }

    public void ReloadFile()
    {
        if (path != null)
        {
            var tmpDomLists = new DomainLists(path);
            domLists = tmpDomLists;
        }
    }
}


public class NickDisable
{
    Dictionary< string, HashSet<string> > disabledNicks =
        new Dictionary< string, HashSet<string> >(StringComparer.OrdinalIgnoreCase);
    object _locker = new object();

    public bool IsNickDisabled(string nick, string channel)
    {
        lock (_locker)
        {
            HashSet<string> nickHashes;
            if (disabledNicks.TryGetValue(channel, out nickHashes))
                return nickHashes.Contains(nick);
            else
                return false;
        }
    }

    public bool Add(string nick, string channel)
    {
        lock (_locker)
        {
            HashSet<string> nickHashes;
            if (disabledNicks.TryGetValue(channel, out nickHashes))
                return nickHashes.Add(nick);
            else
            {
                disabledNicks.Add(channel, new HashSet<string>());
                return disabledNicks[channel].Add(nick);
            }
        }
    }

    public bool Remove(string nick, string channel)
    {
        lock (_locker)
        {
            HashSet<string> nickHashes;
            if (disabledNicks.TryGetValue(channel, out nickHashes))
                return nickHashes.Remove(nick);
            else
                return false;
        }
    }
}