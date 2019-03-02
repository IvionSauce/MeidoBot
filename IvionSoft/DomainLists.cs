using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace IvionSoft
{
    public class DomainLists
    {
        readonly string[] global;
        readonly Dictionary<string, string[]> domainSpecific =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);


        public DomainLists(string path)
        {
            path.ThrowIfNullOrWhiteSpace(nameof(path));

            var tmpGlobal = new List<string>();
            var tmpDomains = new Dictionary< string, List<string> >(StringComparer.OrdinalIgnoreCase);

            using (var fileStream = new StreamReader(path))
            {
                // Applicable domain of the lines yet to read, start of in 'global' mode - meaning that read
                // lines are applicable to all domains. Gets changed whenever instructed to by ":".
                string[] domains = {"_all"};

                while (fileStream.Peek() >= 0)
                {
                    string line = fileStream.ReadLine()
                                            .Trim();
                    
                    // Ignore empty lines or comments.
                    if (string.IsNullOrEmpty(line) || line[0] == '#')
                        continue;
                    
                    if (line[0] == ':')
                        // Remove leading ":" before splitting.
                        domains = line.Substring(1).Split(',');
                    // The rest will be treated as relevant and added to the list.
                    else if (domains.Contains("_all"))
                        tmpGlobal.Add(line);
                    else
                    {
                        foreach(string dom in domains)
                        {
                            var domList = tmpDomains.GetOrAdd(dom);
                            domList.Add(line);
                        }
                    }
                }
            }
            global = tmpGlobal.ToArray();
            foreach (var pair in tmpDomains)
                domainSpecific.Add( pair.Key, pair.Value.ToArray() );
        }


        public bool? IsInDomainList(string domain, string line)
        {
            if (domain == null)
                throw new ArgumentNullException(nameof(domain));
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            string[] domArr;
            if (domainSpecific.TryGetValue(domain, out domArr))
            {
                foreach (string s in domArr)
                    if (line.Contains(s, StringComparison.OrdinalIgnoreCase))
                        return true;

                // Return false if the domain does have an entry, but the line isn't in the list.
                return false;
            }

            // Return null if the domain doesn't even have an entry.
            return null;
        }
        
        public bool IsInGlobalList(string line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            foreach (string s in global)
                if (line.Contains(s, StringComparison.OrdinalIgnoreCase))
                    return true;
            
            return false;
        }
    }
}