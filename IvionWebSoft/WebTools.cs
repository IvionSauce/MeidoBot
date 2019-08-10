using System;
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
        // Try to match \\u0026dur=\d+\\u0026
        static readonly Regex ytRegexp = new Regex(@"(?<=\\\\u0026dur=)\d+.\d+(?=\\\\u0026)");


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
                throw new ArgumentNullException(nameof(htmlString));

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlString);
            HtmlNode titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
            if (titleNode == null)
                return null;
            else
                return SanitizeTitle(titleNode.InnerText);
        }

        static string SanitizeTitle(string title)
        {
            string sanitizedTitle = HttpUtility.HtmlDecode(title);
            return Sanitize.SquashWhitespace(sanitizedTitle);
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
                throw new ArgumentNullException(nameof(htmlString));

            Match timeMatch = ytRegexp.Match(htmlString);
            if (timeMatch.Success)
            {
                var seconds = double.Parse(timeMatch.Value);
                return TimeSpan.FromSeconds(
                    Math.Round(seconds, MidpointRounding.AwayFromZero)
                );
            }
            else
                return TimeSpan.Zero;
        }
    }
}