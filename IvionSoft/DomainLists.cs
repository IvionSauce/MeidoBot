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

        public string Get(string domain, int index)
        {
            string domLow = domain.ToLower();

            List<string> domainList;
            string value = null;

            _rwlock.EnterReadLock();
            if (domainSpecific.TryGetValue(domLow, out domainList))
            {
                if (index < domainList.Count)
                    value = domainList[index];
            }

            _rwlock.ExitReadLock();
            return value;
        }

        public int Add(string line)
        {
            if ( string.IsNullOrWhiteSpace(line) || line[0] == '#' || line[0] == ':' )
                return -1;

            _rwlock.EnterWriteLock();
            globalList.Add(line);
            int index = globalList.Count - 1;

            if (changedSinceLastSave == false)
                changedSinceLastSave = true;

            _rwlock.ExitWriteLock();
            return index;
        }

        public int Add(string domain, string line)
        {
            if ( string.IsNullOrWhiteSpace(line) || line[0] == '#' || line[0] == ':' )
                return -1;

            string domLow = domain.ToLower();

            List<string> domainList;

            _rwlock.EnterWriteLock();
            if (domainSpecific.TryGetValue(domLow, out domainList))
                domainList.Add(line);
            else
            {
                domainSpecific.Add(domLow, new List<string>());
                domainList = domainSpecific[domLow];
                domainList.Add(line);
            }
            int index = domainList.Count - 1;

            if (changedSinceLastSave == false)
                changedSinceLastSave = true;

            _rwlock.ExitWriteLock();
            return index;
        }

        public void Add(string[] domains, string line)
        {
            foreach (string domain in domains)
            {
                Add(domain, line);
            }
        }

        // Returns removed string, returns null if nothing was removed.
        public string Remove(string domain, int index)
        {
            string domLow = domain.ToLower();

            List<string> domainList;
            string value = null;

            _rwlock.EnterWriteLock();
            if (domainSpecific.TryGetValue(domLow, out domainList))
            {
                if (index < domainList.Count)
                {
                    value = domainList[index];
                    domainList.RemoveAt(index);
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
                foreach (string domain in domainSpecific.Keys)
                {
                    fileStream.WriteLine(":{0}", domain);
                    foreach (string pattern in domainSpecific[domain])
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
}