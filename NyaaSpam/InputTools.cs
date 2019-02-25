using System;
using System.Collections.Generic;


static class InputTools
{
    public static List<string> GetPatterns(string patternsStr)
    {
        const string quot = "\"";

        var patterns = new List<string>();
        // If enclosed in quotation marks, add string within the marks verbatim.
        if ( patternsStr.StartsWith(quot, StringComparison.OrdinalIgnoreCase) &&
            patternsStr.EndsWith(quot, StringComparison.OrdinalIgnoreCase) )
        {
            // Slice off the quotation marks.
            string pattern = patternsStr.Substring(1, patternsStr.Length - 2);
            patterns.Add(pattern);
        }
        // Else interpret comma's as seperators between different titles.
        else
        {
            foreach ( string pattern in patternsStr.Split(',') )
                patterns.Add( pattern.Trim() );
        }

        return patterns;
    }


    public static List<int> GetNumbers(string numbersStr)
    {
        var numbers = new List<int>();

        int num;
        foreach (string s in numbersStr.Split(','))
        {
            if (s.Contains("-"))
                numbers.AddRange( GetRange(s) );
            else if (int.TryParse(s, out num))
                numbers.Add(num);
        }

        numbers.Sort();
        return numbers;
    }

    // Return list of ints for a range given as "x-y".
    static List<int> GetRange(string rangeStr)
    {
        var numbers = new List<int>();

        string[] startEnd = rangeStr.Split('-');
        if (startEnd.Length != 2)
            return numbers;

        int start, end;

        if ( int.TryParse(startEnd[0], out start) && int.TryParse(startEnd[1], out end) )
        {
            int num = start;
            while (num <= end)
            {
                numbers.Add(num);
                num++;
            }
        }
        return numbers;
    }


    // Return a reversed and deduplicated list.
    public static List<int> PrepareDeletions(List<int> sortedNums)
    {
        var preparedDeletions = new List<int>(sortedNums.Count);

        int? previous = null;
        int current;
        for (int i = sortedNums.Count - 1; i >= 0; i--)
        {
            current = sortedNums[i];
            if (current != previous)
            {
                preparedDeletions.Add(current);
            }

            previous = current;
        }

        return preparedDeletions;
    }


    public static DateTimeOffset SanitizeDate(DateTimeOffset pastDate, TimeSpan maxElapsed)
    {
        var now = DateTimeOffset.Now;

        if (pastDate < now &&
            (now - pastDate) <= maxElapsed)
        {
            return pastDate;
        }

        return now;
    }
}