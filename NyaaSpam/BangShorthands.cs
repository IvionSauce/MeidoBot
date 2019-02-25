using System;
using System.Collections.Generic;


static class BangShorthands
{
    static readonly Dictionary<string, string[]> bangs;
    const char prefix = '!';


    static BangShorthands()
    {
        var shortHand = new string[] {
            "HS",
            "HS480",
            "HS720",
            "HS1080"
        };

        var expanded = new string[] {
            "[HorribleSubs]",
            "[HorribleSubs] [480p]",
            "[HorribleSubs] [720p]",
            "[HorribleSubs] [1080p]"
        };

        bangs = new Dictionary<string, string[]>(
            shortHand.Length,
            StringComparer.Ordinal
        );

        for (int i = 0; i < shortHand.Length && i < expanded.Length; i++)
        {
            string key = prefix + shortHand[i];
            bangs[key] = expanded[i].Split(' ');
        }
    }


    public static IEnumerable<string> ExpandPattern(string[] pattern)
    {
        foreach (string patternPart in pattern)
        {
            string[] expandedBang;
            // If the word (pattern part) is a bang shorthand expand it and return each constituent.
            if (Shorthand(patternPart, out expandedBang))
            {
                foreach (string bangPart in expandedBang)
                    yield return bangPart;
            }
            // Otherwise just return the word as is.
            else
                yield return patternPart;
        }
    }

    static bool Shorthand(string part, out string[] expanded)
    {
        expanded = null;
        return part.Length > 0 &&
               part[0] == prefix &&
               bangs.TryGetValue(part, out expanded);
    }


    public static string[] GetDescriptions()
    {
        // Get and sort keys.
        var sortedKeys = new string[bangs.Count];
        bangs.Keys.CopyTo(sortedKeys, 0);
        Array.Sort(sortedKeys);

        // Use sorted keys to create a sorted list of descriptions.
        var sortedDescs = new string[sortedKeys.Length];
        for (int i = 0; i < sortedKeys.Length; i++)
        {
            var key = sortedKeys[i];
            sortedDescs[i] = string.Format("{0} -> {1}", key, bangs[key]);
        }

        return sortedDescs;
    }
}