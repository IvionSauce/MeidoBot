using System;
using System.Web;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IvionWebSoft
{
    public static class WikipediaTools
    {
        static readonly Regex paragraphRegexp = new Regex(@"(?i)(?<=<p>).*(?=</p>)");


        /* Matches:
         * Span tags
         * Hyperlink tags
         * Image tags
         * Superscript elements (tags + content)
         * Small elements
         * Bold and italic tags
         */
        static readonly Regex stripRegexp = new Regex(@"(?i)<span ?[^<>]*>|</span>|" +
                                                      @"<a href[^<>]*>|</a>|" +
                                                      @"<img [^<>]* />|" +
                                                      @"<sup[^<>]*>.*?</sup>|" +
                                                      @"<small[^<>]*>.*?</small>|" +
                                                      @"<b>|</b>|" +
                                                      @"<i>|</i>");


        public static string GetFirstParagraph(string htmlDoc)
        {
            if (htmlDoc == null)
                throw new ArgumentNullException("htmlDoc");

            var pMatch = paragraphRegexp.Match(htmlDoc);
            if (pMatch.Success)
            {
                string cleanParagraph = stripRegexp.Replace(pMatch.Value, string.Empty);
                cleanParagraph = HttpUtility.HtmlDecode(cleanParagraph);
                return cleanParagraph;
            }
            else
                return string.Empty;
        }

        public static List<string> GetParagraphs(string htmlDoc)
        {
            if (htmlDoc == null)
                throw new ArgumentNullException("htmlDoc");

            var pMatches = paragraphRegexp.Matches(htmlDoc);
            var paragraphs = new List<string>(pMatches.Count);
            foreach (Match match in pMatches)
            {
                if (!string.IsNullOrEmpty(match.Value))
                {
                    string cleanParagraph = stripRegexp.Replace(match.Value, string.Empty);
                    cleanParagraph = HttpUtility.HtmlDecode(cleanParagraph);
                    paragraphs.Add(cleanParagraph);
                }
            }

            return paragraphs;
        }

    }
}