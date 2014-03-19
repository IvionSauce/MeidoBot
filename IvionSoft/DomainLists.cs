using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace IvionSoft
{
    public class DomainLists
    {
        List<string> globalList = new List<string>();
        Dictionary< string, List<string> > domainSpecific =
            new Dictionary< string, List<string> >(StringComparer.OrdinalIgnoreCase);


        public DomainLists(string file)
        {
            using (var fileStream = new StreamReader(file))
            {
                // Applicable domain of the lines yet to read, start of in 'global' mode - meaning that read
                // lines are applicable to all domains. Gets changed whenever instructed to by ":".
                string[] domain = {"_all"};
                
                while (fileStream.Peek() >= 0)
                {
                    string line = fileStream.ReadLine();
                    
                    // Ignore empty lines or comments.
                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                        continue;
                    else if (line[0] == ':')
                        // Remove leading ":" before splitting.
                        domain = line.Substring(1).Split(',');
                    // The rest will be treated as relevant and added to the list.
                    else
                    {
                        if (domain.Contains("_all"))
                            Add(line);
                        else if (domain.Length == 1)
                            Add(domain[0], line);
                        else
                            Add(domain, line);
                    }
                }
            }
        }


        public bool? IsInDomainList(string domain, string line)
        {
            List<string> domainList;
            if (domainSpecific.TryGetValue(domain, out domainList))
            {
                foreach (string s in domainList)
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
            foreach (string s in globalList)
                if (line.Contains(s, StringComparison.OrdinalIgnoreCase))
                    return true;
            
            return false;
        }


        void Add(string line)
        {
            globalList.Add(line);
        }

        void Add(string domain, string line)
        {
            List<string> domainList;

            if (domainSpecific.TryGetValue(domain, out domainList))
                domainList.Add(line);
            else
            {
                domainSpecific.Add(domain, new List<string>());
                domainList = domainSpecific[domain];
                domainList.Add(line);
            }
        }

        void Add(string[] domains, string line)
        {
            foreach (string domain in domains)
            {
                Add(domain, line);
            }
        }
    }
}