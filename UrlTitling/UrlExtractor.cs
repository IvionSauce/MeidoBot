using System;
using System.Text.RegularExpressions;


namespace WebIrc
{
    public static class UrlExtractor
    {
        static readonly Regex urlRegexp = new Regex(@"(?i)https?://[^\s]+");


        public static string[] Extract(string message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            var urlMatches = urlRegexp.Matches(message);

            string[] results = new string[urlMatches.Count];
            for (int i = 0; i < urlMatches.Count; i++)
                results[i] = urlMatches[i].Value;

            return results;
        }
    }
}