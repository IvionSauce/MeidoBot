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
    volatile DomainLists domLists;
    volatile string path;
    
    
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
        path.ThrowIfNullOrWhiteSpace("path");

        var tmpDomLists = new DomainLists(path);
        domLists = tmpDomLists;
        this.path = path;
    }

    public void ReloadFile()
    {
        // While `path` /could/ change after this check, it will never become null again.
        /// And because `path` is only changed after a successful load, it will be a proper path.
        if (path != null)
        {
            var tmpDomLists = new DomainLists(path);
            domLists = tmpDomLists;
        }
    }
}