﻿using System;
using System.Collections.Generic;


static class BangShorthands
{
    static readonly Dictionary<string, string[]> bangs;


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
            string key = '!' + shortHand[i];
            bangs[key] = expanded[i].Split(' ');
        }
    }


    public static IEnumerable<string> ExpandPattern(string[] pattern)
    {
        foreach (string patternPart in pattern)
        {
            string[] expandedBang;
            // If the word (pattern part) is a bang shorthand expand it and return each constituent.
            if (bangs.TryGetValue(patternPart, out expandedBang))
            {
                foreach (string bangPart in expandedBang)
                    yield return bangPart;
            }
            // Otherwise just return the word as is.
            else
                yield return patternPart;
        }
    }
}