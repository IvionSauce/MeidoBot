using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IvionSoft;
// For `InvalidEnumArgumentException`
using System.ComponentModel;
// For `HttpUtility.HtmlDecode`
using System.Web;
// JSON.NET
using Newtonsoft.Json;


namespace WebHelp
{
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
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// <exception cref="NotSupportedException">Thrown if url does not point to 4chan or foolz.us.</exception>
        /// 
        /// <param name="url">URL pointing to thread.</param>
        public static ChanPost GetThreadOP(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");
            
            Source? src = GetSource(url);
            if (src == null)
                throw new NotSupportedException("Address is not supported.");
            
            var boardAndThread = ExtractBoardAndThreadNo(url, src.Value);
            
            if (boardAndThread.Item2 > 0)
                return GetThreadOP(boardAndThread.Item1, boardAndThread.Item2, src.Value);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Board and/or Thread No. from URL.");
                return new ChanPost(ex);
            }
        }
        
        
        static Source? GetSource(string url)
        {
            if (url.Contains("boards.4chan.org/", StringComparison.OrdinalIgnoreCase))
                return Source.Fourchan;
            else if (url.Contains("archive.foolz.us/", StringComparison.OrdinalIgnoreCase))
                return Source.Foolz;
            else
                return null;
        }


        static Tuple<string, int> ExtractBoardAndThreadNo(string url, Source source)
        {
            GroupCollection groups;
            if (source == Source.Fourchan)
                groups = chanUrlRegexp.Match(url).Groups;
            else
                groups = foolzUrlRegexp.Match(url).Groups;

            if (groups[1].Success && groups[2].Success)
            {
                var threadNo = int.Parse(groups[2].Value);
                return new Tuple<string, int>(groups[1].Value, threadNo);
            }
            else
                return new Tuple<string, int>("", -1);
        }
        
        
        /// <summary>
        /// Determines if address is supported by ChanTools.
        /// </summary>
        /// <returns><c>true</c> if address is supported; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <param name="url">URL</param>
        public static bool IsAddressSupported(string url)
        {
            if (url == null)
                throw new ArgumentNullException("url");
            
            Source? src = GetSource(url);
            if (src == null)
                return false;
            else
                return true;
        }
        
        
        /// <summary>
        /// Get the post of the OP of the thread.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the OP's comment.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if board is null.</exception>
        /// <exception cref="ArgumentException">Thrown if board is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if thread &lt= 0.</exception>
        /// <exception cref="InvalidEnumArgumentException">Thrown if source is unsupported.</exception>
        /// 
        /// <param name="board">Board where thread is located.</param>
        /// <param name="thread">Thread number.</param>
        /// <param name="source">Whether it's a 4chan or foolz.us post.</param>
        public static ChanPost GetThreadOP(string board, int thread, Source source)
        {
            board.ThrowIfNullOrWhiteSpace("board");
            if (thread < 1)
                throw new ArgumentOutOfRangeException("thread", "Can't be 0 or negative.");
            
            // GetJsonString checks whether we got passed a valid Source value.
            WebString json = GetJsonString(board, thread, source);
            if (!json.Success)
                return new ChanPost(json);
            
            dynamic threadJson = JsonConvert.DeserializeObject(json.Document);
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
            
            var opPost = new ChanPost(json,
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
                throw new InvalidEnumArgumentException();
            
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
        /// Disable by passing &lt= 0.</param>
        /// <param name="maxChar">If more characters than maxChar, shorten to maxChar.
        /// Disable by passing &lt= 0.</param>
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


    /// <summary>
    /// Contains a Success bool which tells you if the request succeeded. If an expected exception occurred you can
    /// check the Exception property. If Exception is null and Succes is false it means something went wrong extracting
    /// the board and/or thread number from the URL.
    /// </summary>
    public class ChanPost : WebResource
    {
        public string Board { get; private set; }
        public string BoardName { get; private set; }
        public int ThreadNo { get; private set; }
        public int PostNo { get; private set; }
        public string Subject { get; private set; }
        public string Comment { get; private set; }

        
        public ChanPost(WebResource resource) : base(resource) {}

        public ChanPost(Exception ex) : base(null, false, ex) {}
        
        public ChanPost(WebResource resource,
                        string board, string boardName,
                        int threadNo, int postNo,
                        string subject, string comment) : base(resource)
        {            
            Board = board;
            BoardName = boardName;
            ThreadNo = threadNo;
            PostNo = postNo;
            Subject = subject;
            Comment = comment;
        }
    }
}