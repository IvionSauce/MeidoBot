using System;
using System.Threading;
using System.Runtime.Serialization;
using System.Collections.Generic;
using IvionSoft;


public class Patterns : IDisposable
{
    volatile bool _serialize = true;
    public bool SerializeOnModification
    {
        get { return _serialize; }
        set { _serialize = value; }
    }

    volatile string _path;
    public string FileLocation
    {
        get { return _path; }
        set
        {
            lock (_storageLock)
            {
                if (string.IsNullOrWhiteSpace(value))
                    _path = null;
                else
                    _path = value;
            }
        }
    }

    TimeSpan bufferTime;
    bool pendingWrites;
    DateTimeOffset lastWriteCall;

    Storage<ChannelPatterns> storage = new Storage<ChannelPatterns>();
    readonly object _storageLock = new object();


    public Patterns() : this(TimeSpan.Zero) {}

    public Patterns(TimeSpan bufferTime)
    {
        if (bufferTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException("bufferTime", "Cannot be less than 0.");

        this.bufferTime = bufferTime;
    }


    // ----------------------
    // Add (exclude) pattern.
    // ----------------------

    public int Add(string channel, string pattern)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        if (string.IsNullOrWhiteSpace(pattern))
            return -1;

        string[] split = pattern.Split();
        lock (_storageLock)
        {
            ChannelPatterns chanPat = storage.GetOrSet( channel, () => new ChannelPatterns(channel) );
            chanPat.Patterns.Add( new PatternEntry(split) );
            Write();

            return chanPat.Patterns.Count - 1;
        }
    }

    public int AddExclude(string channel, int assocPat, string exclude)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        int index = -1;
        if (string.IsNullOrWhiteSpace(exclude))
            return index;
        
        string[] split = exclude.Split();
        lock (_storageLock)
        {
            ChannelPatterns chanPat = storage.GetOrSet( channel, () => new ChannelPatterns(channel) );
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


    // ----------------------
    // Get (exclude) pattern.
    // ----------------------

    public string Get(string channel, int index)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return GetOrRemove(false, channel, index);
    }

    public string GetExclude(string channel, int assocPat, int iExclude)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return GetOrRemove(false, channel, assocPat, iExclude);
    }


    // -------------------------
    // Remove (exclude) pattern.
    // -------------------------

    public string Remove(string channel, int index)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return GetOrRemove(true, channel, index);
    }

    public string RemoveExclude(string channel, int assocPat, int iExclude)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return GetOrRemove(true, channel, assocPat, iExclude);
    }


    // --------------------------------------
    // Add/Get/Remove global exclude pattern.
    // --------------------------------------

    public int AddGlobalExclude(string channel, string exclude)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return AddExclude(channel, -1, exclude);
    }

    public string GetGlobalExclude(string channel, int iExclude)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return GetExclude(channel, -1, iExclude);
    }

    public string RemoveGlobalExclude(string channel, int iExclude)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return RemoveExclude(channel, -1, iExclude);
    }


    // -----------------------------------------------------------
    // Private methods for getting or removing (exclude) patterns.
    // -----------------------------------------------------------

    string GetOrRemove(bool remove, string channel, int index)
    {
        string[] pattern = null;
        lock (_storageLock)
        {
            ChannelPatterns chanPat = storage.Get(channel);
            if ( IndexExists(chanPat, index) )
            {
                pattern = chanPat.Patterns[index].IncludePattern;
                if (remove)
                {
                    chanPat.Patterns.RemoveAt(index);
                    Write();
                }
            }
        }
        if (pattern == null)
            return null;
        else
            return string.Join(" ", pattern);
    }

    string GetOrRemove(bool remove, string channel, int assocPat, int iExclude)
    {
        if (iExclude < 0)
            return null;

        string[] exPattern = null;
        lock (_storageLock)
        {
            ChannelPatterns chanPat = storage.Get(channel);
            
            List<string[]> exPatterns;
            if (TryGetExPatterns(chanPat, assocPat, out exPatterns) && iExclude < exPatterns.Count)
            {
                exPattern = exPatterns[iExclude];
                if (remove)
                {
                    exPatterns.RemoveAt(iExclude);
                    Write();
                }
            }
        }
        if (exPattern == null)
            return null;
        else
            return string.Join(" ", exPattern);
    }


    // -------------------------------------------------------------------------------
    // Get all (exclude) patterns belonging to a certain channel or a certain pattern.
    // -------------------------------------------------------------------------------

    public string[] GetPatterns(string channel)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return InternalPatternsGet(channel, false, -1);
    }

    public string[] GetExcludePatterns(string channel, int assocPat)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return InternalPatternsGet(channel, true, assocPat);
    }

    public string[] GetGlobalExcludePatterns(string channel)
    {
        if (channel == null)
            throw new ArgumentNullException("channel");

        return InternalPatternsGet(channel, true, -1);
    }

    // If bool-exclude is false it will return the Include Patterns of passed `channel`.
    // Associated Pattern index is only used when bool-exclude is true, since only when returning those we need to know
    // with which pattern they are associated.
    // When bool-exclude is true and assocPat is negative it will return the Global Exclude Patterns.
    string[] InternalPatternsGet(string channel, bool exclude, int assocPat)
    {
        string[] patternsToRead = null;
        lock (_storageLock)
        {
            ChannelPatterns chanPat = storage.Get(channel);
            // Include Patterns.
            if (chanPat != null && !exclude)
            {
                patternsToRead = new string[chanPat.Patterns.Count];

                for (int i = 0; i < patternsToRead.Length; i++)
                    patternsToRead[i] = string.Join(" ", chanPat.Patterns[i].IncludePattern);
            }
            // (Global) Exclude Patterns.
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


    public List<int> Search(string channel, IEnumerable<string> patternSubstrings)
    {
        if (patternSubstrings == null)
            throw new ArgumentNullException(nameof(patternSubstrings));
        
        var matches = new List<int>();
        var patterns = GetPatterns(channel);

        foreach (string patSub in patternSubstrings)
        {
            matches.Add( Find(patterns, patSub) );
        }

        return matches;
    }

    static int Find(string[] patterns, string patternSubstring)
    {
        for (int i = 0; i < patterns.Length; i++)
        {
            if (patterns[i].Contains(patternSubstring, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // ------------------------------------------------------
    // Helper functions for the above Add/Get/Remove methods.
    // ------------------------------------------------------

    static bool TryGetExPatterns(ChannelPatterns chanPat, int assocPat, out List<string[]> exPatterns)
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

    static bool IndexExists(ChannelPatterns chanPat, int index)
    {
        if (chanPat != null && index < chanPat.Patterns.Count && index >= 0)
            return true;
        else
            return false;
    }

    static bool GlobalRequest(ChannelPatterns chanPat, int index)
    {
        if (chanPat != null && index < 0)
            return true;
        else
            return false;
    }


    // -----------------------------------------------------------------------------
    // Function for determining which channels have a pattern matching passed title.
    // -----------------------------------------------------------------------------

    public string[] PatternMatch(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return new string[0];

        List<string> channels = new List<string>();

        lock (_storageLock)
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
                    // If it matches the pattern and doesn't match any Exclude Patterns associated with the Include
                    // Pattern, add it to the channels list.
                    if (IsMatch(patEntry.IncludePattern, title) &&
                        !ContainsExcludePattern(patEntry, title))
                    {
                        channels.Add(chanPattern.Channel);
                        break;
                    }
                } // foreach
            } // foreach
        } // lock

        return channels.ToArray();
    }


    // ----------------------------------
    // Helper functions for PatternMatch.
    // ----------------------------------

    static bool ContainsGlobalExcludePattern(ChannelPatterns chanPat, string title)
    {
        foreach (string[] pattern in chanPat.GlobalExcludePatterns)
            if ( IsMatch(pattern, title) )
                return true;
        return false;
    }
    
    static bool ContainsExcludePattern(PatternEntry entry, string title)
    {
        foreach (string[] pattern in entry.ExcludePatterns)
            if ( IsMatch(pattern, title) )
                return true;
        return false;
    }


    // ----------------------------------------------------------------------------------------------------------
    // In a sense also a helper function for PatternMatch, but this is the main function deciding whether a title
    // matches a pattern.
    // ----------------------------------------------------------------------------------------------------------

    static bool IsMatch(string[] pattern, string title)
    {
        // Each pattern is an array of constituents. The title needs to contain each of them.
        string tmpTitle = title;
        
        foreach (string s in BangShorthands.ExpandPattern(pattern))
        {
            int startIndex = tmpTitle.IndexOf(s, StringComparison.OrdinalIgnoreCase);
            
            if (startIndex >= 0)
            {
                // Addendum: if we have a match, remove it from the the tmpTitle, this to ensure that if we
                // have a pattern with repeated words it only matches when the title indeed contains
                // multiple instances of that word.
                tmpTitle = tmpTitle.Remove(startIndex, s.Length);
            }
            else
                return false;
        }

        return true;
    }


    // --------------------------
    // Methods for serialization.
    // --------------------------

    // Write depends on the calling method to hold the lock.
    void Write()
    {
        if (FileLocation == null || !SerializeOnModification)
            return;

        if (bufferTime <= TimeSpan.Zero)
            storage.Serialize(FileLocation);
        else
        {
            lastWriteCall = DateTimeOffset.Now;
            if (!pendingWrites)
            {
                pendingWrites = true;
                var t = new Thread(BufferedWrite);
                t.IsBackground = true;
                t.Start();
            }
        }
    }

    void BufferedWrite(object stateInfo)
    {
        DateTimeOffset lastwrite;
        do
        {
            Thread.Sleep(bufferTime);
            lock (_storageLock)
                lastwrite = lastWriteCall;

        } while (lastwrite + bufferTime > DateTimeOffset.Now);

        Serialize();
    }

    public void Serialize()
    {
        lock (_storageLock)
        {
            if (FileLocation != null)
            {
                storage.Serialize(FileLocation);
                pendingWrites = false;
            }
        }
    }

    public void Deserialize()
    {
        lock (_storageLock)
        {
            if (FileLocation != null)
                storage = Storage<ChannelPatterns>.Deserialize(FileLocation);
        }
    }


    public void Dispose()
    {
        lock (_storageLock)
        {
            // If outstanding BufferedWrites, write storage to disk.
            if (pendingWrites)
                Serialize();

            // Disable BufferedWrite.
            bufferTime = TimeSpan.Zero;
            // Disable all writing to disk.
            FileLocation = null;
        }
    }
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/IvionSoft")]
internal class ChannelPatterns
{
    [DataMember]
    public string Channel { get; private set; }

    [DataMember]
    // Each item is a single Include Pattern and its associated Exclude Patterns.
    public List<PatternEntry> Patterns { get; private set; }
    [DataMember]
    // Exclude patterns applicable to all patterns.
    public List<string[]> GlobalExcludePatterns { get; private set; }


    public ChannelPatterns(string channel)
    {
        Channel = channel;
        Patterns = new List<PatternEntry>();
        GlobalExcludePatterns = new List<string[]>();
    }
}

// When I copy out the Pattern Arrays I copy out the references to those arrays. So even if the original list (whether
// it's Patterns or ExcludePatterns or GlobalExcludePatterns) deletes that Pattern Array (the reference to it),
// that which it points to will continue to exist (as long as references to it exist).
// But then, maybe, what it points to can be changed. Since what it points to is an array of strings, each index in the
// array could be changed, assigned a different string.
// Luckily this is no worry, since the Pattern Arrays are either added or deleted in their entirety and are not modified
// during their lifetime.

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/IvionSoft")]
internal class PatternEntry
{
    [DataMember]
    // Single Pattern Array.
    public string[] IncludePattern { get; private set; }
    [DataMember]
    // Multiple Pattern Arrays.
    public List<string[]> ExcludePatterns { get; private set; }


    public PatternEntry(string[] pattern)
    {
        IncludePattern = pattern;
        ExcludePatterns = new List<string[]>();
    }
}