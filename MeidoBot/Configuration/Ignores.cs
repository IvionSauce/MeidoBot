using System;
using System.IO;
using System.Collections.Generic;


namespace MeidoBot
{
    class Ignores
    {
        public int Count
        {
            get
            {
                if (ignores != null)
                    return ignores.Count;
                else
                    return 0;
            }
        }

        readonly HashSet<string> ignores;


        public Ignores() : this(null) {}

        public Ignores(IEnumerable<string> ignoreSources)
        {
            if (ignoreSources != null)
            {
                ignores = new HashSet<string>(ignoreSources, StringComparer.OrdinalIgnoreCase);
                ignores.TrimExcess();
            }
        }

        public static Ignores FromFile(string path, Logger log)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path.Trim() == string.Empty)
                throw new ArgumentException("Cannot be empty or whitespace.", nameof(path));

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (IOException ex)
            {
                log.Verbose("Error loading ignores from {0} ({1})", path, ex.Message);
                return new Ignores();
            }

            var tmp = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    tmp.Add(trimmed);
            }

            var ignores = new Ignores(tmp);
            log.Message("Loaded {0} ignores from {1}", ignores.Count, path);
            return ignores;
        }


        public bool Contains(IrcMsg msg)
        {
            if (ignores != null)
            {
                return ignores.Contains(msg.Nick) ||
                       ignores.Contains(msg.Channel);
            }

            return false;
        }
    }
}