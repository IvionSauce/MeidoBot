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


    /// <summary>
    /// URL-Title comparer.
    /// </summary>
    public class UrlTitleComparer
    {
        HashSet<char> _charIgnore;
        /// <summary>
        /// Set which characters the comparer should ignore.
        /// </summary>
        public HashSet<char> CharIgnore
        {
            get { return _charIgnore; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value", "CharIgnore cannot be null.");
                else
                    _charIgnore = value;
            }
        }

        HashSet<string> _stringIgnore;
        /// <summary>
        /// Set which strings/words the comparer should ignore.
        /// </summary>
        public HashSet<string> StringIgnore
        {
            get { return _stringIgnore; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value", "StringIgnore cannot be null.");
                else
                    _stringIgnore = value;
            }
        }

        const int maxCharCode = 127;


        public UrlTitleComparer()
        {
            // First line is normal punctuation. The second line has punctutation common in titles of webpages.
            // Third line is similar, but contains Unicode characters.
            CharIgnore = new HashSet<char>(new char[] {'.', ',', '!', '?', ':', ';', '&', '\'',
                '-', '|', '<', '>',
                '—', '–', '·', '«', '»'});
            StringIgnore = new HashSet<string>();
        }


        /// <summary>
        /// Compare the title of a webpage and its URL
        /// </summary>
        /// <returns>A double relating how many words from the title occur in the URL. It will range from 0 to 1,
        /// 0 meaning no words from the title are present in the URL and 1 meaning all words from the title are
        /// present in the URL.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url or title is null.</exception>
        /// <param name="url">URL</param>
        /// <param name="title">Title</param>
        public double Similarity(string url, string title)
        {
            if (url == null)
                throw new ArgumentNullException("url");
            else if (title == null)
                throw new ArgumentNullException("title");

            // Replace punctuation with a space, so as to not accidently meld words together. We'll have string.Split
            // take care of any double, or more, spaces.
            StringBuilder cleanedTitle = new StringBuilder();
            foreach (char c in title)
            {
                if (CharIgnore.Contains(c))
                    cleanedTitle.Append(' ');
                else if (c > maxCharCode)
                {
                    cleanedTitle.Append(c);
                    cleanedTitle.Append(' ');
                }
                else
                    cleanedTitle.Append(c);
            }
            string[] words = cleanedTitle.ToString()
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            int totalWords = words.Length;
            int foundWords = 0;
            foreach (string word in words)
            {
                if (StringIgnore.Contains(word))
                    totalWords--;
                else if (url.Contains(word, StringComparison.OrdinalIgnoreCase))
                    foundWords++;
            }

            // If the Total Words count ended up in the negative, return zero.
            // Also safeguard against Divided-By-Zero or `Infinity` result.
            if (totalWords <= 0)
                return 0d;
            else
                return foundWords / (double)totalWords;
        }
    }
}