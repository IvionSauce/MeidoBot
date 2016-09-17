using System;
using System.Text;
using System.Text.RegularExpressions;


enum ReplaceResult
{
    Success,
    NoMatch,
    RegexTimeout
}


class ReplaceAction
{
    public bool ParseSuccess { get; private set; }

    public Regex SearchRegexp { get; private set; }

    public string SearchExpression { get; private set; }
    public string ReplaceExpression { get; private set; }

    public int ReplaceStart { get; private set; }
    public int ReplaceStop { get; private set; }
    public bool CaseSensitive { get; private set; }

    const char EscapeChar = '\\';


    public ReplaceAction(string sedScript)
    {
        if (sedScript.Length >= 2 && sedScript[0] == 's')
        {
            switch (sedScript[1])
            {
                // Allowed seperators.
                case '/':
                case '_':
                case ':':
                case ';':
                case '|':
                Parse(sedScript, 2, sedScript[1]);
                return;
            }
        }
    }


    public ReplaceResult TryReplace(string input, out string output)
    {
        int matchCount = 0;
        int replacedCount = 0;
        Func<Match, string> evaluator = m =>
        {
            matchCount++;
            if ( matchCount >= ReplaceStart &&
                (ReplaceStop == -1 || matchCount <= ReplaceStop) )
            {
                replacedCount++;
                return m.Result(ReplaceExpression);
            }
            return m.ToString();
        };

        output = input;
        if (ParseSuccess)
        {
            try
            {
                output = SearchRegexp.Replace(input, new MatchEvaluator(evaluator));
            }
            catch (RegexMatchTimeoutException)
            {
                return ReplaceResult.RegexTimeout;
            }
        }

        if (replacedCount > 0)
            return ReplaceResult.Success;

        return ReplaceResult.NoMatch;
    }


    int Parse(string sedScript, int start, char seperator)
    {
        int index = start;
        SearchExpression = ParsePattern(sedScript, ref index, seperator, true);
        if (!string.IsNullOrEmpty(SearchExpression))
        {
            ReplaceExpression = ParsePattern(sedScript, ref index, seperator, false);

            // No flags mean replacing only the first occurence and being case sensitive.
            ReplaceStart = 1;
            ReplaceStop = 1;
            CaseSensitive = true;

            index = ParseFlags(sedScript, index);
            SearchRegexp = CreateRegexp(SearchExpression);
        }
        return index;
    }


    int ParseFlags(string sedScript, int index)
    {
        for (; index < sedScript.Length; index++)
        {
            char c = sedScript[index];

            if (char.IsNumber(c))
            {
                int position = c - '0';

                ReplaceStart = position;
                ReplaceStop = position;
            }
            else if (c == 'g')
                ReplaceStop = -1;
            else if (c == 'i')
                CaseSensitive = false;
            else if (char.IsWhiteSpace(c))
                break;
        }

        return index;
    }


    Regex CreateRegexp(string expression)
    {
        RegexOptions opts = RegexOptions.None;
        if (!CaseSensitive)
            opts |= RegexOptions.IgnoreCase;

        var timeout = TimeSpan.FromSeconds(10);

        try
        {
            var regexp = new Regex(expression, opts, timeout);
            ParseSuccess = true;

            return regexp;
        }
        catch (ArgumentException)
        {
            ParseSuccess = false;
            return null;
        }
    }


    static string ParsePattern(string sedScript,
                               ref int index,
                               char seperator,
                               bool mustEndWithSep)
    {
        var tmpPattern = new StringBuilder();
        bool escape = false;

        char c = '\0';
        for (; index < sedScript.Length; index++)
        {
            c = sedScript[index];

            if (escape)
            {
                if (c == seperator || c == EscapeChar)
                    tmpPattern.Append(c);
                else
                    tmpPattern.Append(EscapeChar).Append(c);

                escape = false;
            }
            else
            {
                if (c == EscapeChar)
                    escape = true;
                else if (c == seperator)
                {
                    index++;
                    break;
                }
                else
                    tmpPattern.Append(c);
            }
        }

        if (mustEndWithSep && c != seperator)
            return null;

        return tmpPattern.ToString();
    }
}