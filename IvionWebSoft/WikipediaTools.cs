using System;
using System.Web;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IvionWebSoft
{
    public static class WikipediaTools
    {
        static readonly Regex titleRegexp =
            new Regex(@"(?i)<h1 id=""firstHeading"" class=""firstHeading"" lang=""([^""]+)"">" +
                      @"<span dir=""auto"">([^>]+)</span></h1>");

        static readonly Regex paragraphRegexp = new Regex(@"(?i)(?<=<p>).+(?=</p>)");

        static readonly Regex sectionRegexp =
            new Regex(@"(?i)<h[1-4]><span class=""mw-headline"" id=""([^""]+)"">([^<]+)</span>");


        /* Matches:
         * Span tags
         * Hyperlink tags
         * Abbreviation tags
         * Image tags
         * Superscript elements (tags + content)
         * Small elements
         * Bold and italic tags
         */
        static readonly Regex stripRegexp = new Regex(@"(?i)" +
                                                      @"<span[^>]*>|</span>|" +
                                                      @"<a [^>]*>|</a>|" +
                                                      @"<abbr[^>]*>|</abbr>|" +
                                                      @"<img [^>]*/>|" +
                                                      @"<sup[^>]*>.*?</sup>|" +
                                                      @"<small[^>]*>.*?</small>|" +
                                                      @"<b>|</b>|<i>|</i>");


        public static WikipediaArticle Parse(string htmlDoc)
        {
            if (htmlDoc == null)
                throw new ArgumentNullException("htmlDoc");

            var sectionMatches = sectionRegexp.Matches(htmlDoc);
            var sections = new WikipediaSection[sectionMatches.Count];

            for (int i = 0; i < sectionMatches.Count; i++)
            {
                var match = sectionMatches[i];
                // Peek forward to the next match, since we need to know where to stop looking for paragraphs.
                int nextIndex;
                if (i + 1 < sectionMatches.Count)
                    nextIndex = sectionMatches[i + 1].Index;
                else
                    nextIndex = htmlDoc.Length;

                int length = nextIndex - match.Index;
                var paragraphs = GetParagraphs(htmlDoc, match.Index, length);

                var htmlId = match.Groups[1].Value;
                var title = match.Groups[2].Value;

                sections[i] = new WikipediaSection(title, htmlId, paragraphs);
            }

            var summary = GetSummaryParagraphs(htmlDoc, sectionMatches);
            return new WikipediaArticle(GetTitle(htmlDoc), summary, sections);
        }


        static string[] GetSummaryParagraphs(string htmlDoc, MatchCollection sections)
        {
            if (sections.Count > 0)
                return GetParagraphs(htmlDoc, 0, sections[0].Index);
            else
                return GetParagraphs(htmlDoc, 0, htmlDoc.Length);
        }

        static string[] GetParagraphs(string htmlDoc, int start, int len)
        {
            var paragraphs = new List<string>();
            var match = paragraphRegexp.Match(htmlDoc, start, len);
            while (match.Success)
            {
                paragraphs.Add( SanitizeParagraph(match.Value) );
                match = match.NextMatch();
            }

            return paragraphs.ToArray();
        }

        static string GetTitle(string htmlDoc)
        {
            var match = titleRegexp.Match(htmlDoc);
            if (match.Success)
            {
                return match.Groups[2].Value;
            }
            else
                return string.Empty;
        }


        static string SanitizeParagraph(string para)
        {
            string cleanPara = stripRegexp.Replace(para, string.Empty);
            cleanPara = HttpUtility.HtmlDecode(cleanPara);

            return cleanPara;
        }
    }
}