using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;


namespace IvionSoft
{
    public class DomainListsReader
    {
        protected List<string> globalList = new List<string>();
        protected Dictionary<string,List<string>> domainSpecific = new Dictionary<string, List<string>>();

        protected string filename;
        protected bool changedSinceLastSave = false;

        protected ReaderWriterLockSlim _rwlock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);


        public string Get(int index)
        {
            string value = null;

            _rwlock.EnterReadLock();
            if (index < globalList.Count)
                value = globalList[index];

            _rwlock.ExitReadLock();
            return value;
        }

        public string Get(string channel, int index)
        {
            string chanLow = channel.ToLower();

            List<string> channelList;
            string value = null;

            _rwlock.EnterReadLock();
            if (domainSpecific.TryGetValue(chanLow, out channelList))
            {
                if (index < channelList.Count)
                    value = channelList[index];
            }

            _rwlock.ExitReadLock();
            return value;
        }

        public void Add(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            _rwlock.EnterWriteLock();
            globalList.Add(line);

            if (changedSinceLastSave == false)
                changedSinceLastSave = true;
            _rwlock.ExitWriteLock();
        }

        public void Add(string channel, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            string chanLow = channel.ToLower();

            List<string> channelList;

            _rwlock.EnterWriteLock();
            if (domainSpecific.TryGetValue(chanLow, out channelList))
                channelList.Add(line);
            else
            {
                domainSpecific.Add(chanLow, new List<string>());
                domainSpecific[chanLow].Add(line);
            }

            if (changedSinceLastSave == false)
                changedSinceLastSave = true;
            _rwlock.ExitWriteLock();
        }

        public void Add(string[] channels, string line)
        {
            foreach (string channel in channels)
            {
                Add(channel, line);
            }
        }

        // Returns removed string, returns null if nothing was removed.
        public string Remove(string channel, int index)
        {
            string chanLow = channel.ToLower();

            List<string> lines;
            string value = null;

            _rwlock.EnterWriteLock();
            if (domainSpecific.TryGetValue(chanLow, out lines))
            {
                if (index < lines.Count)
                {
                    value = lines[index];
                    lines.RemoveAt(index);
                }
            }

            if (changedSinceLastSave == false)
                changedSinceLastSave = true;

            _rwlock.ExitWriteLock();
            return value;
        }

        public bool ChangedSinceLastSave()
        {
            _rwlock.EnterReadLock();
            bool value = changedSinceLastSave;
            _rwlock.ExitReadLock();
            return value;
        }

        public void LoadFromFile(string file)
        {
            // Just keep the write lock for the entire reading cycle.
            _rwlock.EnterWriteLock();

            filename = file;
            globalList.Clear();
            domainSpecific.Clear();

            using (var fileStream = new StreamReader(file))
            {
                // Applicable domain of the lines yet to read, start of in 'global' mode - meaning that read
                // lines are applicable to all channels. Gets changed whenever instucted to by ":".
                string[] domain = {"all"};

                while (fileStream.Peek() >= 0)
                {
                    string line = fileStream.ReadLine();

                    // Ignore empty lines or comments.
                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                        continue;
                    // Add channels, delimited by comma's, to the domain.
                    else if (line[0] == ':')
                        // Remove leading ":" before splitting.
                        domain = line.Substring(1).Split(',');
                    // The rest will be treated as relevant and added to the list.
                    else
                    {
                        if (domain[0] == "all")
                            Add(line);
                        else if (domain.Length == 1)
                            Add(domain[0], line);
                        else
                            Add(domain, line);
                    }
                }
            }
            changedSinceLastSave = false;
            _rwlock.ExitWriteLock();
        }

        public void ReloadFile()
        {
            if (filename != null)
                LoadFromFile(filename);
        }
    }


    public class DomainListsReadWriter : DomainListsReader
    {
        public void WriteFile()
        {
            _rwlock.EnterUpgradeableReadLock();
            using (var fileStream = new StreamWriter(filename))
            {
                foreach (string channel in domainSpecific.Keys)
                {
                    fileStream.WriteLine(":{0}", channel);
                    foreach (string pattern in domainSpecific[channel])
                        fileStream.WriteLine(pattern);
                }
            }
            if (changedSinceLastSave == true)
            {
                _rwlock.EnterWriteLock();
                changedSinceLastSave = false;
                _rwlock.ExitWriteLock();
            }
            
            _rwlock.ExitUpgradeableReadLock();
        }
    }


    public class Blacklist : DomainListsReader
    {
        public bool InBlacklist(string url, string channel)
        {
            _rwlock.EnterReadLock();

            // Check the global blacklist.
            foreach (string s in globalList)
            {
                if (url.Contains(s, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check for a channel specific blacklist, since only a minority will have one it will cause the
            // foreach loop to be skipped. (Acting on the assumption that TryGetValue is efficient)
            List<string> channelList;
            if (domainSpecific.TryGetValue(channel.ToLower(), out channelList))
            {
                foreach (string s in channelList)
                {
                    if (url.Contains(s, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            _rwlock.ExitReadLock();
            // If neither return a hit, return false - since it's in neither of the blacklists.
            return false;
        }
    }
}


static class ExtensionMethods
    {
        public static bool Contains(this string source, string value, StringComparison comp)
        {
            return source.IndexOf(value, comp) >= 0;
        }
    }