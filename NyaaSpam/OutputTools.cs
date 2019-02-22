using System;
using System.Text;
using System.Collections.Generic;


static class OutputTools
{
    public static IEnumerable<string> TwoColumns(string[] patterns)
    {
        var half = (int)Math.Ceiling(patterns.Length / 2d);

        int longestLength = 0;
        for (int i = 0; i < half; i++)
        {
            if (patterns[i].Length > longestLength)
                longestLength = patterns[i].Length;
        }

        int j;
        for (int i = 0; i < half; i++)
        {
            j = i + half;
            if (j < patterns.Length)
            {
                var patternLeft = Pad(patterns[i], longestLength);
                var patternRight = patterns[j];

                yield return string.Format("[{0}] {1} [{2}] {3}", i, patternLeft, j, patternRight);
            }
            else
                yield return string.Format("[{0}] {1}", i, patterns[i]);
        }
    }


    static string Pad(string s, int totalLength)
    {
        string padded;
        // Padoru, padoru.
        if (s.Length < totalLength)
        {
            var builder = new StringBuilder(s, totalLength);
            while (builder.Length < totalLength)
            {
                builder.Append(' ');
            }

            padded = builder.ToString();
        }
        // No padoru necessary.
        else
            padded = s;

        return padded;
    }
}