using System;
using System.Threading;
using System.Runtime.Serialization;
using System.Collections.Generic;
using IvionSoft;


public class NyaaPatterns : IDisposable
{
    public bool SerializeOnModification { get; set; }
    public TimeSpan BufferTime { get; set; }
    int ticker;

    Storage<ChannelPatterns> storage = new Storage<ChannelPatterns>();
    object _locker = new object();

    string path;


    public NyaaPatterns()
    {
        SerializeOnModification = true;
        BufferTime = TimeSpan.Zero;
    }


    public int Add(string channel, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return -1;

        string[] split = pattern.Split(' ');
        lock (_locker)
        {
            ChannelPatterns chanPat = GetOrAdd(channel);
            chanPat.Patterns.Add( new PatternEntry(split) );
            Write();

            return chanPat.Patterns.Count - 1;
        }
    }

    public int AddExclude(string channel, int assocPat, string exclude)
    {
        int index = -1;
        if (string.IsNullOrWhiteSpace(exclude))
            return index;
        
        string[] split = exclude.Split(' ');
        lock (_locker)
        {
            ChannelPatterns chanPat = GetOrAdd(channel);

            List<string[]> exPatterns;
            if (TryGetExPatterns(chanPat, assocPat, out exPatterns))
            {
                exPatterns.Add(split);
                Write();

                index = exPatterns.Count - 1;
            }
        }
        return index;
    }

    ChannelPatterns GetOrAdd(string channel)
    {
        ChannelPatterns chanPat = storage.Get(channel);
        if (chanPat == null)
        {
            chanPat = new ChannelPatterns(channel);
            storage.Set(channel, chanPat);
        }
        return chanPat;
    }


    public string Get(string channel, int index)
    {
        string[] pattern = null;
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);
            if ( IndexExists(chanPat, index) )
                pattern = chanPat.Patterns[index].IncludePattern;
        }
        if (pattern == null)
            return null;
        else
            return string.Join(" ", pattern);
    }

    public string GetExclude(string channel, int assocPat, int iExclude)
    {
        string[] exPattern = null;
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);

            List<string[]> exPatterns;
            if (TryGetExPatterns(chanPat, assocPat, out exPatterns) && iExclude < exPatterns.Count)
                exPattern = exPatterns[iExclude];
        }
        if (exPattern == null)
            return null;
        else
            return string.Join(" ", exPattern);
    }


    public string Remove(string channel, int index)
    {
        string[] pattern = null;
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);
            if ( IndexExists(chanPat, index) )
            {
                pattern = chanPat.Patterns[index].IncludePattern;
                chanPat.Patterns.RemoveAt(index);
                Write();
            }
        }
        if (pattern == null)
            return null;
        else
            return string.Join(" ", pattern);
    }

    public string RemoveExclude(string channel, int assocPat, int iExclude)
    {
        string[] exPattern = null;
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);

            List<string[]> exPatterns;
            if (TryGetExPatterns(chanPat, assocPat, out exPatterns) && iExclude < exPatterns.Count)
            {
                exPattern = exPatterns[iExclude];
                exPatterns.RemoveAt(iExclude);
                Write();
            }
        }
        if (exPattern == null)
            return null;
        else
            return string.Join(" ", exPattern);
    }


    public void ClearPatterns(string channel)
    {
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);

            if (chanPat != null)
            {
                chanPat.Patterns = new List<PatternEntry>();
                Write();
            }
        }
    }

    public void ClearAllExcludes(string channel)
    {
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);

            if (chanPat != null)
            {
                foreach (PatternEntry entry in chanPat.Patterns)
                    entry.ExcludePatterns = new List<string[]>();
                Write();
            }
        }
    }

    public void ClearExcludes(string channel, int assocPat)
    {
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);

            if ( IndexExists(chanPat, assocPat) )
            {
                chanPat.Patterns[assocPat].ExcludePatterns = new List<string[]>();
                Write();
            }
            else if ( GlobalRequest(chanPat, assocPat) )
            {
                chanPat.GlobalExcludePatterns = new List<string[]>();
                Write();
            }
        }
    }


    public int AddGlobalExclude(string channel, string exclude)
    {
        return AddExclude(channel, -1, exclude);
    }

    public string GetGlobalExclude(string channel, int iExclude)
    {
        return GetExclude(channel, -1, iExclude);
    }

    public string RemoveGlobalExclude(string channel, int iExclude)
    {
        return RemoveExclude(channel, -1, iExclude);
    }

    public void ClearGlobalExcludes(string channel)
    {
        ClearExcludes(channel, -1);
    }


    public string[] GetPatterns(string channel)
    {
        return InternalPatternsGet(channel, false, -1);
    }
    public string[] GetExcludePatterns(string channel, int assocPat)
    {
        return InternalPatternsGet(channel, true, assocPat);
    }
    public string[] GetGlobalExcludePatterns(string channel)
    {
        return InternalPatternsGet(channel, true, -1);
    }

    // If bool-exclude is false it will return the Include Patterns of passed `channel`.
    // Associated Pattern index is only used when bool-exclude is true, since only when returning those we need to know
    // with which pattern they are associated.
    // When bool-exclude is true and assocPat is negative it will return the Global Exclude Patterns.
    string[] InternalPatternsGet(string channel, bool exclude, int assocPat)
    {
        string[] patternsToRead = null;
        lock (_locker)
        {
            ChannelPatterns chanPat = storage.Get(channel);
            // Get all Include Patterns.
            if (chanPat != null && !exclude)
            {
                patternsToRead = new string[chanPat.Patterns.Count];

                for (int i = 0; i < patternsToRead.Length; i++)
                    patternsToRead[i] = string.Join(" ", chanPat.Patterns[i].IncludePattern);
            }
            else
            {
                List<string[]> exPatterns;
                if (TryGetExPatterns(chanPat, assocPat, out exPatterns))
                {
                    patternsToRead = new string[exPatterns.Count];

                    for (int i = 0; i < patternsToRead.Length; i++)
                        patternsToRead[i] = string.Join(" ", exPatterns[i]);
                }
            }
        }

        if (patternsToRead == null)
            return new string[0];
        else
            return patternsToRead;
    }


    bool TryGetExPatterns(ChannelPatterns chanPat, int assocPat, out List<string[]> exPatterns)
    {
        // Get all Exclude Patterns associated with a certain Include Pattern.
        if ( IndexExists(chanPat, assocPat) )
        {
            exPatterns = chanPat.Patterns[assocPat].ExcludePatterns;
            return true;
        }
        // Get all Global Exclude Patterns.
        else if ( GlobalRequest(chanPat, assocPat) )
        {
            exPatterns = chanPat.GlobalExcludePatterns;
            return true;
        }
        else
        {
            exPatterns = null;
            return false;
        }
    }

    bool IndexExists(ChannelPatterns chanPat, int index)
    {
        if (chanPat != null && index < chanPat.Patterns.Count && index >= 0)
            return true;
        else
            return false;
    }

    bool GlobalRequest(ChannelPatterns chanPat, int index)
    {
        if (chanPat != null && index < 0)
            return true;
        else
            return false;
    }


    public string[] PatternMatch(string title)
    {
        List<string> channels = new List<string>();

        lock (_locker)
        {
            // Iterate over the channels.
            foreach (ChannelPatterns chanPattern in storage.GetAll())
            {
                // If title matches a Global Exclude Pattern, skip to the next channel.
                if ( ContainsGlobalExcludePattern(chanPattern, title) )
                    continue;

                // Iterate over the patterns associated with channel.
                foreach (PatternEntry patEntry in chanPattern.Patterns)
                {
                    if ( IsMatch(patEntry.IncludePattern, title) )
                    {
                        // If it matches the pattern and doesn't match any Exclude Patterns associated with the Include
                        // Pattern, add it to the channels list.
                        if ( !ContainsExcludePattern(patEntry, title) )
                        {
                            channels.Add(chanPattern.Channel);
                            break;
                        }
                    }
                }
            }
        }

        return channels.ToArray();
    }


    bool ContainsGlobalExcludePattern(ChannelPatterns chanPat, string title)
    {
        foreach (string[] pattern in chanPat.GlobalExcludePatterns)
            if ( IsMatch(pattern, title) )
                return true;
        return false;
    }
    
    bool ContainsExcludePattern(PatternEntry entry, string title)
    {
        foreach (string[] pattern in entry.ExcludePatterns)
            if ( IsMatch(pattern, title) )
                return true;
        return false;
    }

    bool IsMatch(string[] pattern, string title)
    {
        // Each pattern is an array of constituents. If the title contains one subtract 1 from the
        // countdown. When the countdown reaches 0 it means all constituents were found in the title, so
        // add the channel.
        string tmpTitle = title;
        int countdown = pattern.Length;
        
        foreach (string s in pattern)
        {
            int startIndex = tmpTitle.IndexOf(s, StringComparison.OrdinalIgnoreCase);
            
            if (startIndex >= 0)
            {
                // Addendum: if we have a match, remove it from the the tmpTitle, this to ensure that if we
                // have a pattern with repeated words it only matches when the title indeed contains
                // multiple instances of that word.
                tmpTitle = tmpTitle.Remove(startIndex, s.Length);
                countdown--;
            }
        }
        
        if (countdown <= 0)
            return true;
        else
            return false;
    }


    // Write depends on the calling method to hold the lock.
    void Write()
    {
        if (path == null || !SerializeOnModification)
            return;

        if (BufferTime <= TimeSpan.Zero)
            storage.Serialize(path);
        else
        {
            ticker++;
            ThreadPool.QueueUserWorkItem(BufferedWrite);
        }
    }

    // Since Write returns after spawning this thread we cannot be sure of the lock's state, reacquire.
    void BufferedWrite(object stateInfo)
    {
        Thread.Sleep(BufferTime);

        lock (_locker)
        {
            ticker--;
            if (ticker == 0 && path != null)
                storage.Serialize(path);
        }
    }


    public void Serialize()
    {
        lock (_locker)
        {
            if (path != null)
                storage.Serialize(path);
        }
    }

    public void Serialize(string path)
    {
        lock (_locker)
            storage.Serialize(path);
    }

    public void Deserialize(string path)
    {
        lock (_locker)
        {
            this.path = path;
            storage = Storage<ChannelPatterns>.Deserialize(path);
        }
    }


    public void Dispose()
    {
        lock (_locker)
        {
            // If outstanding BufferedWrites, write storage to disk.
            if (ticker > 0 && path != null)
                storage.Serialize(path);
            // Disable BufferedWrite.
            ticker = -1;
            BufferTime = TimeSpan.Zero;
        }
    }
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/IvionSoft")]
class ChannelPatterns
{
    [DataMember]
    public string Channel { get; private set; }

    [DataMember]
    // Each item is a single Include Pattern and its associated Exclude Patterns.
    public List<PatternEntry> Patterns { get; set; }
    [DataMember]
    public List<string[]> GlobalExcludePatterns { get; set; }


    public ChannelPatterns(string channel)
    {
        Channel = channel;
        Patterns = new List<PatternEntry>();
        GlobalExcludePatterns = new List<string[]>();
    }
}

// When I copy out the Pattern Arrays I copy out the pointers to those arrays. So even if the original list (whether
// it's Patterns or ExcludePatterns or GlobalExcludePatterns) deletes that Pattern Array (the pointer to it), that which
// it points to will continue to exist (as long as references/pointers to it exist).
// But then, maybe, what it points to can be changed. Since what it points to is an array of strings, each index in the
// array could be changed, assigned a different string.
// Luckily this is no worry, since the Pattern Arrays are either added or deleted in their entirety and are not modified
// during their lifetime.

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/IvionSoft")]
class PatternEntry
{
    [DataMember]
    // Single Pattern Array.
    public string[] IncludePattern { get; set; }
    [DataMember]
    // Multiple Pattern Arrays.
    public List<string[]> ExcludePatterns { get; set; }


    public PatternEntry(string[] pattern)
    {
        IncludePattern = pattern;
        ExcludePatterns = new List<string[]>();
    }
}