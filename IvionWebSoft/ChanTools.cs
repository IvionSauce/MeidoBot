using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
// For `HttpUtility.HtmlDecode`
using System.Web;
// JSON.NET
using Newtonsoft.Json;


namespace IvionWebSoft
{
    public static class FourChan
    {
        static readonly Regex chanUrlRegexp =
            new Regex(@"(?i)boards\.4chan\.org/([a-z0-9]+)/thread/(\d+)(?:[^#]*#[pq]?(\d+))?");

        // Span tags
        // Hyperlink tags
        // <wbr> tags
        // Bold and italic tags
        static readonly Regex fixPostRegexp = new Regex(@"(?i)<span[^>]*>|</span>|" +
                                                        @"<a [^>]*>|</a>|" +
                                                        @"<wbr>|" +
                                                        @"<b>|</b>|<i>|</i>");


        /// <summary>
        /// Get the post pointed to by the URL.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the post.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
        /// <param name="url">URL pointing to a thread and/or post.</param>
        public static ChanPost GetPost(string url)
        {            
            var boardPost = Extract(url);
            if (boardPost.Item3 > 0)
                return GetPost(boardPost.Item1, boardPost.Item2, boardPost.Item3);
            else if (boardPost.Item2 > 0)
                return GetThreadPost(boardPost.Item1, boardPost.Item2);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Board and/or Thread No. from URL.");
                return new ChanPost(null, ex);
            }
        }


        /// <summary>
        /// Extract board, thread number and optional post number from URL.
        /// </summary>
        /// <returns>Tuple containing: board, thread number and post number (last is optional).
        /// If unable to extract the default return will be (string.Empty, -1, -1).</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
        /// <param name="url">URL pointing to a thread and/or post.</param>
        public static Tuple<string, int, int> Extract(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");

            return ChanTools.Extract(chanUrlRegexp, url);
        }


        /// <summary>
        /// Get opening post associated with thread number.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the post.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if board is null.</exception>
        /// <exception cref="ArgumentException">Thrown if board is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if threadNo &lt= 0.</exception>
        /// 
        /// <param name="board">Board where thread/post is located.</param>
        /// <param name="threadNo">Thread number.</param>
        public static ChanPost GetThreadPost(string board, int threadNo)
        {
            return GetPost(board, threadNo, threadNo);
        }


        /// <summary>
        /// Get post associated with post number.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the post.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if board is null.</exception>
        /// <exception cref="ArgumentException">Thrown if board is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if threadNo and/or postNo &lt= 0.</exception>
        /// 
        /// <param name="board">Board where thread/post is located.</param>
        /// <param name="threadNo">Thread number.</param>
        /// <param name="postNo">Post number.</param>
        public static ChanPost GetPost(string board, int threadNo, int postNo)
        {
            board.ThrowIfNullOrWhiteSpace("board");
            if (threadNo < 1)
                throw new ArgumentOutOfRangeException("thread", "Cannot be 0 or negative.");
            else if (postNo < 1)
                throw new ArgumentOutOfRangeException("post", "Cannot be 0 or negative.");

            var jsonReq = string.Format("http://a.4cdn.org/{0}/res/{1}.json", board, threadNo);
            var json = WebString.Download(jsonReq);
            if (!json.Success)
                return new ChanPost(json);
            
            dynamic threadJson = JsonConvert.DeserializeObject(json.Document);
            string subject = null;
            string comment = null;

            // Simple linear search, seems fast enough for the 100s of posts it needs to look through.
            foreach (var post in threadJson.posts)
            {
                if (post.no == postNo)
                {
                    subject = post.sub;
                    if (!string.IsNullOrEmpty(subject))
                        subject = HttpUtility.HtmlDecode(subject);
                    
                    comment = post.com;
                    if (!string.IsNullOrEmpty(comment))
                        comment = Fix4chanPost(comment);

                    break;
                }
            }

            return new ChanPost(json.Location,
                                board, ChanTools.GetBoardName(board),
                                threadNo, postNo,
                                subject, comment);
        }

        static string Fix4chanPost(string post)
        {
            string fixedPost = post.Replace("<br>", "\n");
            
            fixedPost = fixPostRegexp.Replace(fixedPost, "");
            fixedPost = HttpUtility.HtmlDecode(fixedPost);
            
            return fixedPost;
        }
    }


    public static class ArchiveMoe
    {
        static readonly Regex archiveUrlRegexp =
            new Regex(@"(?i)archive\.moe/([a-z0-9]+)/thread/(\d+)(?:[^#]*#[pq]?(\d+))?");


        /// <summary>
        /// Get the post pointed to by the URL.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the post.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
        /// <param name="url">URL pointing to a thread and/or post.</param>
        public static ChanPost GetPost(string url)
        {            
            var boardPost = Extract(url);
            if (boardPost.Item3 > 0)
                return GetPost(boardPost.Item1, boardPost.Item3);
            else if (boardPost.Item2 > 0)
                return GetPost(boardPost.Item1, boardPost.Item2);
            else
            {
                var ex = new FormatException("Unable to extract (valid) Board and/or Thread No. from URL.");
                return new ChanPost(null, ex);
            }
        }


        /// <summary>
        /// Extract board, thread number and optional post number from URL.
        /// </summary>
        /// <returns>Tuple containing: board, thread number and post number (last is optional).
        /// If unable to extract the default return will be (string.Empty, -1, -1).</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
        /// <param name="url">URL pointing to a thread and/or post.</param>
        public static Tuple<string, int, int> Extract(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");

            return ChanTools.Extract(archiveUrlRegexp, url);
        }


        /// <summary>
        /// Get post associated with post number.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the post</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if board is null.</exception>
        /// <exception cref="ArgumentException">Thrown if board is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if postNo &lt= 0.</exception>
        /// 
        /// <param name="board">Board where post is located.</param>
        /// <param name="postNo">Post number.</param>
        public static ChanPost GetPost(string board, int postNo)
        {
            board.ThrowIfNullOrWhiteSpace("board");
            if (postNo < 1)
                throw new ArgumentOutOfRangeException("post", "Cannot be 0 or negative.");
            
            var jsonReq = string.Format("http://archive.moe/_/api/chan/post/?board={0}&num={1}", board, postNo);
            var json = WebString.Download(jsonReq);
            if (!json.Success)
                return new ChanPost(json);
            
            dynamic postJson = JsonConvert.DeserializeObject(json.Document);
            int threadNo = postJson.thread_num;
            string subject = postJson.title;
            string comment = postJson.comment_sanitized;
            
            return new ChanPost(json.Location,
                                board, ChanTools.GetBoardName(board),
                                threadNo, postNo,
                                subject, comment);
        }
    }


    public static class ChanTools
    {        
        static readonly Regex spoilerRegexp = new Regex(@"(?i)(<s>|\[spoiler\])(.*?)(</s>|\[/spoiler])");

        static readonly Regex quotRegexp = new Regex(@">>\d+\n");

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
        /// Gets the full name of the board.
        /// </summary>
        /// <returns>Full board name. If unknown, return string.Empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown if board is null.</exception>
        /// <param name="board">Short board designation.</param>
        static public string GetBoardName(string board)
        {
            if (board == null)
                throw new ArgumentNullException("board");

            string name;
            if (boardMapping.TryGetValue(board, out name))
                return name;
            else
                return string.Empty;
        }


        /// <summary>
        /// Removes post quotations.
        /// </summary>
        /// <returns>Post without the post quotations.</returns>
        /// <exception cref="ArgumentNullException">Thrown if post is null.</exception>
        /// <param name="post">Content of a post.</param>
        public static string RemovePostQuotations(string post)
        {
            if (post == null)
                throw new ArgumentNullException("post");

            return quotRegexp.Replace(post, string.Empty);
        }

        
        /// <summary>
        /// Removes spoiler tags.
        /// </summary>
        /// <returns>Post without spoiler tags.</returns>
        /// <exception cref="ArgumentNullException">Thrown if post is null.</exception>
        /// <param name="post">Content of a post.</param>
        public static string RemoveSpoilerTags(string post)
        {
            return ReplaceSpoilerTags(post, string.Empty, string.Empty);
        }
        
        /// <summary>
        /// Replaces spoiler tags.
        /// </summary>
        /// <returns>Post with spoiler tags replaced.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if post is null.</exception>
        /// 
        /// <param name="post">Content of a post.</param>
        /// <param name="beginReplacement">What to replace the opening spoiler tag with.</param>
        /// <param name="endReplacement">What to replace the closing spoiler tag with.</param>
        public static string ReplaceSpoilerTags(string post, string beginReplacement, string endReplacement)
        {
            if (post == null)
                throw new ArgumentNullException("post");
            
            return spoilerRegexp.Replace(post, string.Concat(beginReplacement, "$2", endReplacement));
        }


        internal static Tuple<string, int, int> Extract(Regex urlRegexp, string url)
        {
            string board = string.Empty;
            int threadNo = -1;
            int postNo = -1;

            var match = urlRegexp.Match(url);
            if (match.Success)
            {
                board = match.Groups[1].Value;
                threadNo = int.Parse(match.Groups[2].Value);

                if (match.Groups[3].Success)
                    postNo = int.Parse(match.Groups[3].Value);
            }

            return new Tuple<string, int, int>(board, threadNo, postNo);
        }
    }
}