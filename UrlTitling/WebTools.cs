using System;
using System.Text;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
            Match timeMatch = ytRegexp.Match(htmlString);
            if (timeMatch.Success)
                return int.Parse(timeMatch.Value);
            else
                return -1;
        }

        public static string SimpleGetString(string url)
        {
            var wc = new WebClient();
            try
            {
                string jsonStr = wc.DownloadString(url);
                return jsonStr;

            }
            catch (WebException ex)
            {
                Console.WriteLine("WebException in SimpleGetWebString: " + ex.Message);
                return null;
            }
        }
    }


    public class UrlTitleComparer
    {
        // First line is normal punctuation. The second line has punctutation common in titles of webpages.
        // Third line is similar, but contains Unicode characters.
        HashSet<char> _charIgnore = new HashSet<char>(new char[] {'.', ',', '!', '?', ':', ';', '&', '\'',
            '-', '|', '<', '>',
            '—', '–', '·', '«', '»'}
        );
        public HashSet<char> CharIgnore
        {
            get { return _charIgnore; }
            set { _charIgnore = value; }
        }

        HashSet<string> _stringIgnore = new HashSet<string>();
        public HashSet<string> StringIgnore
        {
            get { return _stringIgnore; }
            set { _stringIgnore = value; }
        }

        const int maxCharCode = 127;


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
                    totalWords -= 1;
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

        static readonly Regex chanUrlRegexp = new Regex(@"boards\.4chan\.org/([a-z0-9]+)/res/(\d+)");
        static readonly Regex foolzUrlRegexp = new Regex(@"archive\.foolz\.us/([a-z0-9]+)/thread/(\d+)");
 
        // <span class="quote">Quote</span>
        // <a href="bla">Bla</a>
        // <wbr>
        static readonly Regex fixPostRegexp = new Regex(@"<span ?[^<>]*>|</span>|" +
                                                        @"<a href=""[^<>""]*"">|</a>|" +
                                                        @"<wbr>");

        static readonly Regex spoilerRegexp =  new Regex(@"(<s>|\[spoiler\])(.*?)(</s>|\[/spoiler])");


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
            if (url.Contains("boards.4chan.org/", StringComparison.OrdinalIgnoreCase))
                return GetThreadOP(url, Source.Fourchan);
            else if (url.Contains("archive.foolz.us/", StringComparison.OrdinalIgnoreCase))
                return GetThreadOP(url, Source.Foolz);
            else
                return null;
        }

        public static ChanPost GetThreadOP(string url, Source source)
        {
            string[] boardAndThread = GetBoardAndThreadNo(url, source);

            if (boardAndThread == null)
                return null;
            else
                return GetThreadOP(boardAndThread[0], int.Parse(boardAndThread[1]), source);
        }


        public static ChanPost GetThreadOP(string board, int thread, Source source)
        {
            string jsonStr = GetJsonString(board, thread, source);
            if (string.IsNullOrWhiteSpace(jsonStr))
                return null;

            dynamic threadJson = JsonConvert.DeserializeObject(jsonStr);

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

            var threadOp = new ChanPost();
            threadOp.Board = board;
            threadOp.BoardName = GetBoardName(board);
            threadOp.PostNo = threadOp.ThreadNo = thread;
            threadOp.Subject = opSubject;
            threadOp.Comment = opComment;

            return threadOp;
        }

        static string GetJsonString(string board, int thread, Source source)
        {
            // Construct query.
            string jsonReq;
            if (source == Source.Fourchan)
                jsonReq = string.Format("http://a.4cdn.org/{0}/res/{1}.json", board, thread);
            else if (source == Source.Foolz)
                jsonReq = string.Format("http://archive.foolz.us/_/api/chan/post/?board={0}&num={1}", board, thread);
            else
                throw new ArgumentException("Source is not supported");
            
            // Debug
            Console.WriteLine("JSON Request: {0}", jsonReq);

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


    public class ChanPost
    {
        public string Board { get; set; }
        public string BoardName { get; set; }
        public int ThreadNo { get; set; }
        public int PostNo { get; set; }
        public string Subject { get; set; }
        public string Comment { get; set; }
    }


    public static class DanboTools
    {
        static readonly Regex danboUrlRegexp = new Regex(@"donmai.us/posts/(\d+)");

        // Matches the "_(source)" part that is sometimes present with character tags.
        static readonly Regex charSourceRegexp = new Regex(@"_\([^) ]+\)");


        public static Dictionary<string, string> GetPostInfo(string url)
        {
            string postNo = GetPostNo(url);

            if (postNo == null)
                return null;
            else
                return GetPostInfo(int.Parse(postNo));
        }

        public static Dictionary<string, string> GetPostInfo(int postNo)
        {
            var jsonReq = string.Format("http://sonohara.donmai.us/posts/{0}.json", postNo);

            // Debug
            Console.WriteLine("JSON Request: {0}", jsonReq);

            // Download the JSON into a string.
            string jsonStr = WebTools.SimpleGetString(jsonReq);
            // If we couldn't get it, return null.
            if (jsonStr == null)
                return null;

            dynamic postJson = JsonConvert.DeserializeObject(jsonStr);

            var postInfo = new Dictionary<string, string>();
            postInfo["id"] = postNo.ToString();
            postInfo["characters"] = postJson.tag_string_character;
            postInfo["copyrights"] = postJson.tag_string_copyright;
            postInfo["artists"] = postJson.tag_string_artist;

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

        public static string CleanupCharacterTags(string charTags)
        {
            return charSourceRegexp.Replace(charTags, "");
        }
    }


    static class ExtensionMethods
    {
        public static bool Contains(this string source, string value, StringComparison comp)
        {
            return source.IndexOf(value, comp) >= 0;
        }
    }
}