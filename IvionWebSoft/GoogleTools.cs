using System;
using System.Web;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace IvionWebSoft
{
    public static class GoogleTools
    {
        // Example: <div class="jfp3ef"><a href="/url?q=https://en.wikipedia.org/wiki/Cowboy_Bebop&amp;sa=U&amp;ved=2ahUKEwiz7L_i-sPiAhWI66QKHX-DBGIQFjAMegQIChAB&amp;usg=AOvVaw3SnjtMGuu5NiDY2AWChYaP"><div class="BNeawe vvjwJb AP7Wnd">Cowboy Bebop - Wikipedia</div>
        static readonly Regex resultsRegexp = new Regex(
            @"<div class=""jfp3ef"">\s*" +
            @"<a href=""/url\?q=([^<>""]+)&amp;sa=[^<>""]+"">\s*" +
            @"<div class=""BNeawe vvjwJb AP7Wnd"">(.+?)</div>");
        
        static readonly Regex boldRegExp = new Regex(
            @"(<b>)(.*?)(</b>)");


        public static SearchResults Search(string query)
        {
            if (query == null)
                throw new ArgumentNullException("query");

            var results = WebString.Download( GoogleUrl(query) );
            if (!results.Success)
                return new SearchResults(results);

            var matches = resultsRegexp.Matches(results.Document);
            var parsedResults = ParseMatches(matches);

            return new SearchResults(results, Deduplication(parsedResults));
        }

        static string GoogleUrl(string searchQuery)
        {
            const string searchUrl = "https://www.google.com/search?q={0}&ie=utf-8&oe=utf-8&hl=en";
            return string.Format(searchUrl, Uri.EscapeDataString(searchQuery));
        }

        static SearchResult[] ParseMatches(MatchCollection matches)
        {
            var parsedResults = new SearchResult[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                var groups = matches[i].Groups;
                if (groups[1].Success && groups[2].Success)
                {
                    // We'll need to decode the URL/URI as well, since stuff is double escaped (ugh).
                    // Surprisingly UrlDecode decodes it correctly (yay).
                    var uri = new Uri( HttpUtility.UrlDecode(groups[1].Value) );
                    var title = HttpUtility.HtmlDecode(groups[2].Value);

                    parsedResults[i] = new SearchResult(uri, title);
                }
            }

            return parsedResults;
        }

        static SearchResult[] Deduplication(SearchResult[] results)
        {
            if (results.Length > 1)
            {
                var dedupResults = new List<SearchResult>(results.Length);
                // Add the first result, since that's skipped in the loop below.
                dedupResults.Add(results[0]);
                // Only check whether the first address is duplicated, this is the only case I've seen,
                // most likely because we also try to get the result that's in a box above the regular results.
                Uri firstLink = results[0].Address;
                for (int i = 1; i < results.Length; i++)
                {
                    if (firstLink.Equals(results[i].Address))
                        continue;
                    
                    dedupResults.Add(results[i]);
                }

                return dedupResults.ToArray();
            }

            return results;
        }


        public static string RemoveBoldTags(string title)
        {
            return ReplaceBoldTags(title, string.Empty, string.Empty);
        }

        public static string ReplaceBoldTags(string title, string beginReplacement, string endReplacement)
        {
            if (title == null)
                throw new ArgumentNullException("title");

            return boldRegExp.Replace(title, string.Concat(beginReplacement, "$2", endReplacement));
        }
    }


    public class SearchResults : WebResource, IEnumerable<SearchResult>
    {
        public SearchResult[] Results { get; private set; }
        public int Count
        {
            get { return Results.Length; }
        }


        public SearchResults(WebResource resource) : base(resource)
        {
            Results = new SearchResult[0];
        }

        public SearchResults(WebResource resource,
                             SearchResult[] results) : base(resource)
        {
            Results = results;
        }


        public IEnumerator<SearchResult> GetEnumerator()
        {
            return ((IEnumerable<SearchResult>) Results).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    public class SearchResult
    {
        public Uri Address { get; private set; }
        public string Title { get; private set; }


        public SearchResult(Uri address, string title)
        {
            Address = address;
            Title = title;
        }
    }
}