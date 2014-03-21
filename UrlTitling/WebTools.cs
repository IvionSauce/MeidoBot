using System;
using System.Text;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WebResources;
// For `HttpUtility.HtmlDecode`
using System.Web;
// HTML Agility Pack
using HtmlAgilityPack;
// JSON.NET
using Newtonsoft.Json;

namespace WebToolsModule
{
    /// <summary>
    /// Generic web tools.
    /// </summary>
    public static class WebTools
    {
        // groups leading/trailing whitespace and intertextual newlines and carriage returns.
        static readonly Regex titleRegexp = new Regex(@"^\s+|\s+$|[\n\r]+");
        // Try to match "length_seconds": \d+[,}]
        static readonly Regex ytRegexp = new Regex(@"(?<=""length_seconds"":\s)\d+(?=[,}])");


        /// <summary>
        /// Gets the title of a webpage.
        /// </summary>
        /// <returns>The title, with leading/trailing whitespace removed. In-title newlines and carriage
        /// returns are also removed. HTML escape codes are decoded to 'normal' characters.
        /// Returns null if the title can't be found.</returns>
        /// <param name="htmlString">String content of an HTML page.</param>
        public static string GetTitle(string htmlString)
        {
            if (string.IsNullOrWhiteSpace(htmlString))
                return null;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlString);
            HtmlNode titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null)
                return SanitizeTitle(titleNode.InnerText);
            else
                return null;
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
        /// <returns>The duration in seconds. If unable to determine duration it will return -1.</returns>
        /// <param name="htmlString">String content of the HTML page with the YouTube video.</param>
        public static int GetYoutubeTime(string htmlString)
        {
            if (htmlString == null)
                return -1;

            Match timeMatch = ytRegexp.Match(htmlString);
            if (timeMatch.Success)
                return int.Parse(timeMatch.Value);
            else
                return -1;
        }

        public static WebString SimpleGetString(string url)
        {
            if (url == null)
                throw new ArgumentNullException("url");

            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (UriFormatException ex)
            {
                return new WebString(null, ex);
            }

            var wc = new WebClient();
            try
            {
                var document = wc.DownloadString(uri);
                return new WebString(uri, document);

            }
            catch (WebException ex)
            {
                return new WebString(uri, ex);
            }
        }
    }


    /// <summary>
    /// URL-Title comparer.
    /// </summary>
    public class UrlTitleComparer
    {
        /// <summary>
        /// Set which characters the comparer should ignore.
        /// </summary>
        public HashSet<char> CharIgnore { get; set; }
        /// <summary>
        /// Set which strings/words the comparer should ignore.
        /// </summary>
        public HashSet<string> StringIgnore { get; set; }

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
        public double CompareUrlAndTitle(string url, string title)
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
                .Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            int totalWords = words.Length;
            int foundWords = 0;
            foreach (string word in words)
            {
                if (StringIgnore.Contains(word))
                {
                    totalWords--;
                    continue;
                }
                if (url.Contains(word, StringComparison.OrdinalIgnoreCase))
                    foundWords++;
            }

            // If the Total Words count /somehow/ ended up in the negative, return zero.
            // Also safeguard against Divided-By-Zero or `Infinity` result.
            if (totalWords <= 0)
                return 0d;
            else
                return foundWords / (double)totalWords;
        }
    }


    /// <summary>
    /// Collection of tools dealing with 4chan and/or Foolz.us.
    /// </summary>
    public static class ChanTools
    {
        [Flags]
        public enum Source
        {
            Fourchan,
            Foolz
        }

        static readonly Regex chanUrlRegexp = new Regex(@"(?i)boards\.4chan\.org/([a-z0-9]+)/res/(\d+)");
        static readonly Regex foolzUrlRegexp = new Regex(@"(?i)archive\.foolz\.us/([a-z0-9]+)/thread/(\d+)");
 
        // <span class="quote">Quote</span>
        // <a href="bla">Bla</a>
        // <wbr>
        static readonly Regex fixPostRegexp = new Regex(@"(?i)<span ?[^<>]*>|</span>|" +
                                                        @"<a href=""[^<>""]*"">|</a>|" +
                                                        @"<wbr>");

        static readonly Regex spoilerRegexp =  new Regex(@"(?i)(<s>|\[spoiler\])(.*?)(</s>|\[/spoiler])");


        static readonly Dictionary<string, string> boardMapping = new Dictionary<string, string>()
        {
            // Japanese Culture
            {"a", "Anime & Manga"},
            {"c", "Anime/Cute"},
            {"w", "Anime/Wallpapers"},
            {"m", "Mecha"},
            {"cgl", "Cosplay & EGL"},
            {"cm", "Cute/Male"},
            {"f", "Flash"},
            {"n", "Transportation"},
            {"jp", "Otaku Culture"},
            {"vp", "Pokémon"},
            // Interests
            {"v", "Video Games"},
            {"vg", "Video Game Generals"},
            {"vr", "Retro Games"},
            {"co", "Comics & Cartoons"},
            {"g", "Technology"},
            {"tv", "Television & Film"},
            {"k", "Weapons"},
            {"o", "Auto"},
            {"an", "Animals & Nature"},
            {"tg", "Traditional Games"},
            {"sp", "Sports"},
            {"asp", "Alternative Sports"},
            {"sci", "Science & Math"},
            {"int", "International"},
            {"out", "Outdoors"},
            {"toy", "Toys"},
            {"biz", "Business & Finance"},
            // Creative
            {"i", "Oekaki"},
            {"po", "Papercraft & Origami"},
            {"p", "Photography"},
            {"ck", "Food & Cooking"},
            {"ic", "Artwork/Critique"},
            {"wg", "Wallpapers/General"},
            {"mu", "Music"},
            {"fa", "Fashion"},
            {"3", "3DCG"},
            {"gd", "Graphic Design"},
            {"diy", "Do-It-Yourself"},
            {"wsg", "Worksafe GIF"},
            // Adult
            {"s", "Sexy Beautiful Women"},
            {"hc", "Hardcore"},
            {"hm", "Handsome Men"},
            {"h", "Hentai"},
            {"e", "Ecchi"},
            {"u", "Yuri"},
            {"d", "Hentai/Alternative"},
            {"y", "Yaoi"},
            {"t", "Torrents"},
            // Rapidshares doesn't follow the 4chan.org/[board] standard.
            // rs.4chan.org
            {"hr", "High Resolution"},
            {"gif", "Adult GIF"},
            // Other
            {"trv", "Travel"},
            {"fit", "Fitness"},
            {"x", "Paranormal"},
            {"lit", "Literature"},
            {"adv", "Advice"},
            {"lgbt", "LGBT"},
            {"mlp", "Pony"},
            // Misc.
            {"b", "Random"},
            {"r", "Request"},
            {"r9k", "ROBOT9001"},
            {"pol", "Politically Incorrect"},
            {"soc", "Cams & Meetups"},
            {"s4s", "Shit 4chan Says"}
        };


        /// <summary>
        /// Get the post of the OP of the thread.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the OP's comment.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url does not point to 4chan or foolz.us.</exception>
        /// <param name="url">URL pointing to thread.</param>
        public static ChanPost GetThreadOP(string url)
        {
            if (url == null)
                throw new ArgumentNullException("url");

            if (url.Contains("boards.4chan.org/", StringComparison.OrdinalIgnoreCase))
                return GetThreadOP(url, Source.Fourchan);
            else if (url.Contains("archive.foolz.us/", StringComparison.OrdinalIgnoreCase))
                return GetThreadOP(url, Source.Foolz);
            else
                throw new ArgumentException("Address not supported", "url");
        }

        /// <summary>
        /// Get the post of the OP of the thread.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the OP's comment.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if source is not supported.</exception>
        /// <param name="url">URL pointing to thread.</param>
        /// <param name="source">Whether it's a 4chan or foolz.us post.</param>
        public static ChanPost GetThreadOP(string url, Source source)
        {
            if (url == null)
                throw new ArgumentNullException("url");

            string[] boardAndThread = GetBoardAndThreadNo(url, source);

            if (boardAndThread == null)
                return new ChanPost();
            else
                return GetThreadOP(boardAndThread[0], int.Parse(boardAndThread[1]), source);
        }


        /// <summary>
        /// Get the post of the OP of the thread.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the OP's comment.</returns>
        /// <exception cref="ArgumentNullException">Thrown if board is null.</exception>
        /// <exception cref="ArgumentException">Thrown if source is not supported.</exception>
        /// <param name="board">Board where thread is located.</param>
        /// <param name="thread">Thread number.</param>
        /// <param name="source">Whether it's a 4chan or foolz.us post.</param>
        public static ChanPost GetThreadOP(string board, int thread, Source source)
        {
            if (board == null)
                throw new ArgumentNullException("board");

            WebString jsonStr = GetJsonString(board, thread, source);
            if (!jsonStr.Succes)
                return new ChanPost(jsonStr);

            dynamic threadJson = JsonConvert.DeserializeObject(jsonStr.Document);
            string opSubject, opComment;
            if (source == Source.Fourchan)
            {
                opSubject = threadJson.posts[0].sub;
                opComment = threadJson.posts[0].com;
                if (opComment != null)
                    opComment = Fix4chanPost(opComment);
            }
            else
            {
                opSubject = threadJson.title;
                opComment = threadJson.comment_sanitized;

                if (opSubject == "")
                    opSubject = null;
                if (opComment == "")
                    opComment = null;
            }

            var opPost = new ChanPost(jsonStr,
                                      board, GetBoardName(board),
                                      thread, thread,
                                      opSubject, opComment);

            return opPost;
        }

        static WebString GetJsonString(string board, int thread, Source source)
        {
            // Construct query.
            string jsonReq;
            if (source == Source.Fourchan)
                jsonReq = string.Format("http://a.4cdn.org/{0}/res/{1}.json", board, thread);
            else if (source == Source.Foolz)
                jsonReq = string.Format("http://archive.foolz.us/_/api/chan/post/?board={0}&num={1}", board, thread);
            else
                throw new ArgumentException("Source is not supported");


            return WebTools.SimpleGetString(jsonReq);
        }

        static string Fix4chanPost(string post)
        {
            // Turn <br>'s into newlines.
            string fixedPost = post.Replace("<br>", "\n");

            fixedPost = fixPostRegexp.Replace(fixedPost, "");
            fixedPost = HttpUtility.HtmlDecode(fixedPost);

            return fixedPost;
        }


        static string[] GetBoardAndThreadNo(string url, Source source)
        {
            GroupCollection groups;
            if (source == Source.Fourchan)
                groups = chanUrlRegexp.Match(url).Groups;
            else
                groups = foolzUrlRegexp.Match(url).Groups;

            if (groups[1].Success && groups[2].Success)
                return new string[] {groups[1].Value, groups[2].Value};
            else
                return null;
        }

        static string GetBoardName(string board)
        {
            string name;
            if (boardMapping.TryGetValue(board, out name))
                return name;
            else
                return "Unknown";
        }

        /// <summary>
        /// Shortens the post and replaces newlines with spaces. Multiple newlines are squashed.
        /// </summary>
        /// <returns>The shortened post. Returns post as-is if null or empty.</returns>
        /// <param name="post">String content of a post.</param>
        /// <param name="maxLines">If more lines than maxLines, shorten to maxLines.
        /// Disable by passing <= 0.</param>
        /// <param name="maxChar">If more characters than maxChar, shorten to maxChar.
        /// Disable by passing <= 0.</param>
        /// <param name="contSymbol">String to append to the returned string if it was shortened.</param>
        public static string ShortenPost(string post, int maxLines, int maxChar, string contSymbol)
        {
            if (string.IsNullOrEmpty(post))
                return post;

            bool shortenLines = maxLines > 0;
            bool shortenChars = maxChar > 0;

            string[] postLines = post.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);

            string shortPost;
            if (shortenLines && postLines.Length > maxLines)
                shortPost = string.Join(" ", postLines, 0, maxLines);
            else
                shortPost = string.Join(" ", postLines);

            if (shortenChars && shortPost.Length > maxChar)
            {
                shortPost = shortPost.Substring(0, maxChar);
                return string.Concat(shortPost, contSymbol);
            }
            else if (shortenLines && postLines.Length > maxLines)
                return string.Concat(shortPost, " ", contSymbol);
            else
                return shortPost;
        }

        /// <summary>
        /// Removes spoiler tags.
        /// </summary>
        /// <returns>String content of the post without spoiler tags.
        /// Returns post as-is if null or empty.</returns>
        /// <param name="post">String content of a post.</param>
        public static string RemoveSpoilerTags(string post)
        {
            return ReplaceSpoilerTags(post, "", "");
        }

        /// <summary>
        /// Replaces spoiler tags.
        /// </summary>
        /// <returns>String content of the post with spoiler tags replaced.
        /// Returns post as-is if null or empty.</returns>
        /// <param name="post">String content of a post.</param>
        /// <param name="beginReplacement">What to replace the opening spoiler tag with.</param>
        /// <param name="endReplacement">What to replace the closing spoiler tag with.</param>
        public static string ReplaceSpoilerTags(string post, string beginReplacement, string endReplacement)
        {
            if (string.IsNullOrEmpty(post))
                return post;

            return spoilerRegexp.Replace(post, string.Concat(beginReplacement, "$2", endReplacement));
        }
    }


    internal static class ExtensionMethods
    {
        internal static bool Contains(this string source, string value, StringComparison comp)
        {
            return source.IndexOf(value, comp) >= 0;
        }
    }


    /// <summary>
    /// Collection of tools dealing with Danbooru.
    /// </summary>
    public static class DanboTools
    {
        static readonly Regex danboUrlRegexp = new Regex(@"(?i)donmai.us/posts/(\d+)");

        // Matches the "_(source)" part that is sometimes present with character tags.
        static readonly Regex charSourceRegexp = new Regex(@"_\([^) ]+\)");


        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="DanboPost">DanboPost</see> detailing a post.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <param name="url">URL pointing to a post.</param>
        public static DanboPost GetPostInfo(string url)
        {
            if (url == null)
                throw new ArgumentNullException("url");

            string postNo = GetPostNo(url);

            if (postNo == null)
                return new DanboPost();
            else
                return GetPostInfo(int.Parse(postNo));
        }

        /// <summary>
        /// Get info of a Danbooru post.
        /// </summary>
        /// <returns><see cref="DanboPost">DanboPost</see> detailing a post.</returns>
        /// <param name="postNo">Post number.</param>
        public static DanboPost GetPostInfo(int postNo)
        {
            var jsonReq = string.Format("http://sonohara.donmai.us/posts/{0}.json", postNo);
            WebString jsonStr = WebTools.SimpleGetString(jsonReq);
            if (!jsonStr.Succes)
                return new DanboPost(jsonStr);

            dynamic postJson = JsonConvert.DeserializeObject(jsonStr.Document);

            string[] copyrights = postJson.tag_string_copyright.Split(' ');
            string[] characters = postJson.tag_string_character.Split(' ');
            string[] artists = postJson.tag_string_artist.Split(' ');
            string[] other = postJson.tag_string_general.Split(' ');
            string[] all = postJson.tag_string.Split(' ');

            DanboPost.Rating rated;
            if (postJson.rating == "s")
                rated = DanboPost.Rating.Safe;
            else if (postJson.rating == "q")
                rated = DanboPost.Rating.Questionable;
            else
                rated = DanboPost.Rating.Explicit;

            var postInfo = new DanboPost(jsonStr, postNo,
                                         copyrights, characters, artists, other,
                                         all, rated);

            return postInfo;
        }


        static string GetPostNo(string url)
        {
            GroupCollection groups = danboUrlRegexp.Match(url).Groups;

            if (groups[1].Success == true)
                return groups[1].Value;
            else
                return null;
        }


        /// <summary>
        /// Converts an array of tags into a tag string.
        /// </summary>
        /// <returns>A string of tags.</returns>
        /// <exception cref="ArgumentNullException">Thrown if tags is null.</exception>
        /// <param name="tags">An array of tags.</param>
        /// <param name="maxTags">Don't make the returned string contain more tags than maxTags.
        /// Disable by passing <= 0.</param>
        public static string TagArrayToString(string[] tags, int maxTags)
        {
            return TagArrayToString(tags, maxTags, "");
        }

        /// <summary>
        /// Converts an array of tags into a tag string.
        /// </summary>
        /// <returns>A string of tags.</returns>
        /// <exception cref="ArgumentNullException">Thrown if tags is null.</exception>
        /// <param name="tags">An array of tags.</param>
        /// <param name="maxTags">Don't make the returned string contain more tags than maxTags.
        /// Disable by passing <= 0.</param>
        /// <param name="contSymbol">String to append to the returned string if it was shortened.</param>
        public static string TagArrayToString(string[] tags, int maxTags, string contSymbol)
        {
            string[] shortened = ShortenTagArray(tags, maxTags);
            var tagStr = string.Join(" ", shortened);

            if (shortened.Length != tags.Length)
                return string.Concat(tagStr, contSymbol);
            else
                return tagStr;
        }

        /// <summary>
        /// Shortens an array of tags.
        /// </summary>
        /// <returns>An array of strings equal or shorter than amount.</returns>
        /// <exception cref="ArgumentNullException">Thrown if tags is null.</exception>
        /// <param name="tags">An array of tags.</param>
        /// <param name="amount">Maximum amount of items the array should have.</param>
        public static string[] ShortenTagArray(string[] tags, int amount)
        {
            if (tags == null)
                throw new ArgumentNullException("tags");

            if ( amount > 0 && tags.Length > amount )
            {
                var shortened = new string[amount];
                for (int i = 0; i < amount; i++)
                    shortened[i] = tags[i];

                return shortened;
            }
            else
                return tags;
        }


        /// <summary>
        /// Cleans up the character tags. Removes the "_(source)" part of the tags.
        /// Returns charTags as-is if null or empty.
        /// </summary>
        /// <returns>Cleaned up character tags.</returns>
        /// <param name="charTags">A tag string of character tags.</param>
        public static string CleanupCharacterTags(string charTags)
        {
            if (string.IsNullOrEmpty(charTags))
                return charTags;
            else
                return charSourceRegexp.Replace(charTags, "");
        }

        /// <summary>
        /// Cleans up the character tags. Removes the "_(source)" part of the tags.
        /// Modifies charTags in-place.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if charTags is null.</exception>
        /// <param name="charTags">A tag array of character tags.</param>
        public static void CleanupCharacterTags(string[] charTags)
        {
            if (charTags == null)
                throw new ArgumentNullException("charTags");

            for (int i = 0; i < charTags.Length; i++)
                charTags[i] = charSourceRegexp.Replace(charTags[i], "");
        }
    }
}