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
    public static class WebTools
    {
        // groups leading/trailing whitespace and intertextual newlines and carriage returns.
        static readonly Regex titleRegexp = new Regex(@"^\s+|\s+$|[\n\r]+");
        // Try to match "length_seconds": \d+[,}]
        static readonly Regex ytRegexp = new Regex(@"(?<=""length_seconds"":\s)\d+(?=[,}])");


        // Get the contents of <title> of the HTML corresponding to passed HTML string.
        // Returns null if <title> cannot be found.
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

        // Return length in seconds of a YouTube URL. Returns -1 if the length cannot be found.
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


    public class UrlTitleComparer
    {
        public HashSet<char> CharIgnore { get; set; }
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


        // Compare the title of webpage and its URL. It will return a double relating how many words from the title
        // occur in the URL. It will range from 0 to 1, 0 meaning no words from the title ara present in the URL and
        // 1 meaning all words from the title are present in the URL.
        public double CompareUrlAndTitle(string url, string title)
        {
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


        // Overloaded methods to wrap GetThreadOP and present a nice interface to the outside.
        public static ChanPost GetThreadOP(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Cannot be null, empty or whitespace", "url");

            if (url.Contains("boards.4chan.org/", StringComparison.OrdinalIgnoreCase))
                return GetThreadOP(url, Source.Fourchan);
            else if (url.Contains("archive.foolz.us/", StringComparison.OrdinalIgnoreCase))
                return GetThreadOP(url, Source.Foolz);
            else
                throw new ArgumentException("Address not supported", "url");
        }

        public static ChanPost GetThreadOP(string url, Source source)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Cannot be null, empty or whitespace", "url");

            string[] boardAndThread = GetBoardAndThreadNo(url, source);

            if (boardAndThread == null)
                return new ChanPost();
            else
                return GetThreadOP(boardAndThread[0], int.Parse(boardAndThread[1]), source);
        }


        public static ChanPost GetThreadOP(string board, int thread, Source source)
        {
            if (string.IsNullOrWhiteSpace(board))
                throw new ArgumentException("Cannot be null, empty or whitespace", "board");

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


        public static string RemoveSpoilerTags(string post)
        {
            return ReplaceSpoilerTags(post, "", "");
        }

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


    public static class DanboTools
    {
        static readonly Regex danboUrlRegexp = new Regex(@"(?i)donmai.us/posts/(\d+)");

        // Matches the "_(source)" part that is sometimes present with character tags.
        static readonly Regex charSourceRegexp = new Regex(@"_\([^) ]+\)");


        public static DanboPost GetPostInfo(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Cannot be null, empty or whitespace", "url");

            string postNo = GetPostNo(url);

            if (postNo == null)
                return new DanboPost();
            else
                return GetPostInfo(int.Parse(postNo));
        }

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


        public static string ShortenTagList(string tags, int amount, string contSymbol)
        {
            if (string.IsNullOrEmpty(tags) || amount < 1)
                return tags;

            string[] tagArray = tags.Split(' ');
            if (tagArray.Length > amount)
            {
                var newTags = string.Join(" ", tagArray, 0, amount);
                return string.Concat(newTags, contSymbol);
            }
            else
                return tags;
        }

        public static string[] ShortenTagArray(string[] tags, int amount)
        {
            if (tags == null)
                return new string[0];
            else if ( amount > 0 && tags.Length > amount )
                return tags.Slice(0, amount);
            else
                return tags;
        }


        public static string CleanupCharacterTags(string charTags)
        {
            if (string.IsNullOrEmpty(charTags))
                return charTags;
            else
                return charSourceRegexp.Replace(charTags, "");
        }

        // In-place modification.
        public static void CleanupCharacterTags(string[] charTags)
        {
            if (charTags == null)
                return;

            for (int i = 0; i < charTags.Length; i++)
                charTags[i] = charSourceRegexp.Replace(charTags[i], "");
        }


        // http://www.dotnetperls.com/array-slice
        static T[] Slice<T>(this T[] source, int start, int end)
        {
            // Handles negative ends.
            if (end < 0)
            {
                end = source.Length + end;
            }
            int len = end - start;
            
            // Return new array.
            T[] res = new T[len];
            for (int i = 0; i < len; i++)
            {
                res[i] = source[i + start];
            }
            return res;
        }
    }
}