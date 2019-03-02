using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
// For `HttpUtility.HtmlDecode`
using System.Web;
// HTML Agility Pack
using HtmlAgilityPack;


namespace IvionWebSoft
{
    /// <summary>
    /// Generic web tools.
    /// </summary>
    public static class WebTools
    {
        // Leading/trailing whitespace and intertextual newlines and carriage returns.
        static readonly Regex titleRegexp = new Regex(@"^\s+|\s+$|[\n\r]+");
        // Try to match "length_seconds": \d+[,}]
        static readonly Regex ytRegexp = new Regex(@"(?<=""length_seconds"":\s?"")\d+(?=""[,}])");


        /// <summary>
        /// Gets the title of a webpage.
        /// </summary>
        /// <returns>The title, with leading/trailing whitespace removed. In-title newlines and carriage
        /// returns are also removed. HTML escape codes are decoded to 'normal' characters.
        /// Returns null if the title can't be found.</returns>
        /// <exception cref="ArgumentNullException">Thrown if htmlString is null.</exception>
        /// <param name="htmlString">String content of an (X)HTML page.</param>
        public static string GetTitle(string htmlString)
        {
            if (htmlString == null)
                throw new ArgumentNullException("htmlString");

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlString);
            HtmlNode titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
            if (titleNode == null)
                return null;
            else
                return SanitizeTitle(titleNode.InnerText);
        }

        // Remove newlines, carriage returns, leading and trailing whitespace.
        // Convert HTML Character References to 'normal' characters.
        static string SanitizeTitle(string title)
        {
            string sanitizedTitle;

            sanitizedTitle = HttpUtility.HtmlDecode(title);
            sanitizedTitle = titleRegexp.Replace(sanitizedTitle, "");

            return sanitizedTitle;
        }


        /// <summary>
        /// Gets the duration of a YouTube movie.
        /// </summary>
        /// <returns>The duration as TimeSpan.</returns>
        /// <exception cref="ArgumentNullException">Thrown if htmlString is null.</exception>
        /// <param name="htmlString">String content of the (X)HTML page with the YouTube video.</param>
        public static TimeSpan GetYoutubeTime(string htmlString)
        {
            if (htmlString == null)
                throw new ArgumentNullException("htmlString");

            Match timeMatch = ytRegexp.Match(htmlString);
            if (timeMatch.Success)
            {
                var seconds = int.Parse(timeMatch.Value);
                return TimeSpan.FromSeconds(seconds);
            }
            else
                return TimeSpan.Zero;
        }
    }
}