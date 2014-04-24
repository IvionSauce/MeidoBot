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

        static FormatException formatEx =
            new FormatException("Unable to extract (valid) Board and/or Thread No. from URL.");
        
        
        /// <summary>
        /// Get the post of the OP of the thread.
        /// </summary>
        /// <returns><see cref="ChanPost">ChanPost</see> detailing the OP's comment.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is empty or whitespace.</exception>
        /// 
        /// <param name="url">URL pointing to thread.</param>
        public static ChanPost GetThreadOP(string url)
        {
            url.ThrowIfNullOrWhiteSpace("url");
            
            Source? src = GetSource(url);
            if (src == null)
                return new ChanPost(formatEx);
            
            var boardAndThread = ExtractBoardAndThreadNo(url, src.Value);
            
            if (boardAndThread.Item2 > 0)
                return GetThreadOP(src.Value, boardAndThread.Item1, boardAndThread.Item2);
            else
                return new ChanPost(formatEx);
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


        public static Tuple<string, int> ExtractBoardAndThreadNo(string url, Source source)
        {
            url.ThrowIfNullOrWhiteSpace("url");

            GroupCollection groups;
            switch(source)
            {
            case Source.Fourchan:
                groups = chanUrlRegexp.Match(url).Groups;
                break;
            case Source.Foolz:
                groups = foolzUrlRegexp.Match(url).Groups;
                break;
            default:
                throw new InvalidEnumArgumentException();
            }

            if (groups[1].Success && groups[2].Success)
            {
                var threadNo = int.Parse(groups[2].Value);
                return new Tuple<string, int>(groups[1].Value, threadNo);
            }
            else
                return new Tuple<string, int>(string.Empty, -1);
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
        public static ChanPost GetThreadOP(Source source, string board, int thread)
        {
            board.ThrowIfNullOrWhiteSpace("board");
            if (thread < 1)
                throw new ArgumentOutOfRangeException("thread", "Can't be 0 or negative.");
            
            // GetJsonString checks whether we got passed a valid Source value.
            WebString json = GetJsonString(source, board, thread);
            if (!json.Success)
                return new ChanPost(json);
            
            dynamic threadJson = JsonConvert.DeserializeObject(json.Document);
            string opSubject, opComment;
            if (source == Source.Fourchan)
            {
                opSubject = threadJson.posts[0].sub;
                opComment = threadJson.posts[0].com;
                if (!string.IsNullOrEmpty(opComment))
                    opComment = Fix4chanPost(opComment);
            }
            else
            {
                opSubject = threadJson.title;
                opComment = threadJson.comment_sanitized;
            }
            // Make sure subject and comment are not null.
            opSubject = opSubject ?? string.Empty;
            opComment = opComment ?? string.Empty;
            
            var opPost = new ChanPost(json,
                                      board, GetBoardName(board),
                                      thread, thread,
                                      opSubject, opComment);
            
            return opPost;
        }
        
        static WebString GetJsonString(Source source, string board, int thread)
        {
            // Construct query.
            string jsonReq;
            switch(source)
            {
            case Source.Fourchan:
                jsonReq = string.Format("http://a.4cdn.org/{0}/res/{1}.json", board, thread);
                break;
            case Source.Foolz:
                jsonReq = string.Format("http://archive.foolz.us/_/api/chan/post/?board={0}&num={1}", board, thread);
                break;
            default:
                throw new InvalidEnumArgumentException();
            }
            
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
                return string.Empty;
        }

        
        /// <summary>
        /// Removes spoiler tags.
        /// </summary>
        /// <returns>String content of the post without spoiler tags.</returns>
        /// <exception cref="ArgumentNullException">Thrown if post is null.</exception>
        /// <param name="post">String content of a post.</param>
        public static string RemoveSpoilerTags(string post)
        {
            return ReplaceSpoilerTags(post, "", "");
        }
        
        /// <summary>
        /// Replaces spoiler tags.
        /// </summary>
        /// <returns>String content of the post with spoiler tags replaced.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if post is null.</exception>
        /// 
        /// <param name="post">String content of a post.</param>
        /// <param name="beginReplacement">What to replace the opening spoiler tag with.</param>
        /// <param name="endReplacement">What to replace the closing spoiler tag with.</param>
        public static string ReplaceSpoilerTags(string post, string beginReplacement, string endReplacement)
        {
            if (post == null)
                throw new ArgumentNullException("post");
            
            return spoilerRegexp.Replace(post, string.Concat(beginReplacement, "$2", endReplacement));
        }
    }


    /// <summary>
    /// Contains a Success bool which tells you if the request succeeded. If an expected exception occurred you can
    /// check the Exception property.
    /// </summary>
    public class ChanPost : WebResource
    {
        /// <summary>
        /// Short board designation, without the slashes.
        /// </summary>
        public string Board { get; private set; }
        /// <summary>
        /// Full board name.
        /// </summary>
        public string BoardName { get; private set; }
        /// <summary>
        /// Thread number.
        /// </summary>
        public int ThreadNo { get; private set; }
        /// <summary>
        /// Post number.
        /// </summary>
        public int PostNo { get; private set; }
        /// <summary>
        /// Subject of the post. Will be empty if no subject.
        /// </summary>
        public string Subject { get; private set; }
        /// <summary>
        /// Comment/message of the post. Will be empty if no comment.
        /// </summary>
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