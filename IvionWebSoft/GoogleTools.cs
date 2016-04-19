using System;
using System.Web;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IvionWebSoft
{
    public static class GoogleTools
    {
        // Look upon this Regex and despair, yay.
        // Example: <h3 class="r"><a href="/url?q=http://epguides.com/CowboyBebop/&amp;sa=U&amp;ei=5WawU5D2MsO0PN6sgfAJ&amp;ved=0CB8QFjAB&amp;usg=AFQjCNGOTF8vsoyU186bgOVhXeTnfz7yEA"><b>Cowboy Bebop</b> (a Titles &amp; <b>Air Dates</b> Guide) - Epguides.com</a></h3>
        static readonly Regex resultsRegexp = new Regex(
            @"<h3 class=""r"">\s*<a href=""/url\?q=([^<>""]*)&amp;sa[^<>""]*""[^<>]*>(.*?)</a>\s*</h3>");

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

            return new SearchResults(results, parsedResults);
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