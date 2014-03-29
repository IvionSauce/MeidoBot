using System;
using System.Text;
using System.Collections.Generic;
// For dealing with SSL/TLS.
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
// My personal help for dealing with the pesky world of HTTP/HTML and the idiots that misuse it.
using WebHelp;

namespace WebIrc
{
    public class WebToIrc
    {
        readonly HtmlEncodingHelper htmlEncHelper = new HtmlEncodingHelper();
        readonly UrlTitleComparer urlTitleComp = new UrlTitleComparer();

        public static double Threshold { get; set; }
        public static CookieContainer Cookies { get; private set; }

        public ChanHandler Chan { get; private set; }
        public DanboHandler Danbo { get; private set; }


        static WebToIrc()
        {
            // State we want to use our ACCEPT ALL implementation.
            ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificates;

            Cookies = new CookieContainer();
        }
        public WebToIrc()
        {
            Chan = new ChanHandler();
            Danbo = new DanboHandler();
        }


        public string GetWebInfo(string url)
        {
            // Danbooru handling.
            if (url.Contains("donmai.us/posts/", StringComparison.OrdinalIgnoreCase))
            {
                return Danbo.PostToIrc(url);
            }
            // Foolz and 4chan handling.
            else if (ChanTools.IsAddressSupported(url))
            {
                return Chan.ThreadTopicToIrc(url);
            }


            // ----- Above: Don't Need HTML and/or Title -----
            // -----------------------------------------------
            string htmlString = GetHtmlContent(url);
            if (htmlString == null)
                // Something went wrong in GetHtmlString, which should've already printed the error.
                return null;

            string htmlTitle = WebTools.GetTitle(htmlString);
            if (htmlTitle == null)
            {
                Console.WriteLine(url + " -- No <title> found");
                return null;
            }
            // -----------------------------------------
            // ----- Below: Need HTML and/or Title -----


            // Youtube handling.
            if (url.Contains("youtube.com/watch?", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://youtu.be/", StringComparison.OrdinalIgnoreCase))
            {
                // If duration can be found, change the html info to include that. Else return normal info.
                int ytTime = WebTools.GetYoutubeTime(htmlString);
                if (ytTime > 0)
                {
                    int ytMinutes = ytTime / 60;
                    int ytSeconds = ytTime % 60;
                    return string.Format("[ {0} ] [{1}:{2:00}]", htmlTitle, ytMinutes, ytSeconds);
                }
                else
                    return string.Format("[ {0} ]", htmlTitle);
            }
            // Other URLs.
            else
            {                
                double urlTitleSimilarity = urlTitleComp.CompareUrlAndTitle(url, htmlTitle);
                Console.WriteLine("URL-Title Similarity: " + urlTitleSimilarity);

                // If the URL and Title are too similar, don't return HTML info for printing to IRC.
                if (urlTitleSimilarity < Threshold)
                    return string.Format("[ {0} ]", htmlTitle);
                else
                    return null;
            }
        }

        string GetHtmlContent(string url)
        {
            var webStr = htmlEncHelper.GetWebString(url, Cookies);

            if (!webStr.Success && webStr.Exception.InnerException is CookieException)
            {
                Console.WriteLine("--- CookieException caught! Trying without cookies.");
                webStr = htmlEncHelper.GetWebString(url);
            }

            if (webStr.Success)
            {
                Console.WriteLine("(HTTP) \"{0}\" -> {1} ; (HTML) \"{2}\" -> {3}",
                                  htmlEncHelper.HeadersCharset, htmlEncHelper.EncHeaders,
                                  htmlEncHelper.HtmlCharset, htmlEncHelper.EncHtml);
                return webStr.Document;
            }

            else if (webStr.Exception is UrlNotHtmlException)
                Console.WriteLine("Not (X)HTML: " + url);
            else
                webStr.HandleError(url);

            return null;
        }


        // Implement an ACCEPT ALL Certificate Policy (for SSL).
        // What we're going to do is not security sensitive. SSL or not, we don't care,
        // we just want to get the HTML file and get out - we're not sending any sensitive data.
        static bool TrustAllCertificates(object sender, X509Certificate cert, X509Chain chain,
                                         SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }


    static class ExtensionMethods
    {
        public static bool Contains(this string source, string value, StringComparison comp)
        {
            return source.IndexOf(value, comp) >= 0;
        }


        public static void HandleError(this WebResource resource, string url)
        {
            if (resource.Exception.Message == null)
                Console.WriteLine("!!! Unknown error, contact software author.");
            else
            {
                const string errorMsg = "--- Error getting {0} ({1})";
                if (resource.Location == null)
                    Console.WriteLine(errorMsg, url, resource.Exception.Message);
                else
                    Console.WriteLine(errorMsg, resource.Location, resource.Exception.Message);
            }
        }
    }


    public class ChanHandler
    {
        public int TopicMaxLines { get; set; }
        public int TopicMaxChars { get; set; }
        public string ContinuationSymbol { get; set; }

        public string ThreadTopicToIrc(string url)
        {
            ChanPost opPost = ChanTools.GetThreadOP(url);

            if (opPost.Success)
            {
                string topic = null;
                // Prefer subject as topic, if the post has one. Else reform the message into a topic.
                // If a post has neither subject or comment/message, return null.
                if (opPost.Subject != null)
                    topic = opPost.Subject;
                else if (opPost.Comment != null)
                {
                    topic = ChanTools.RemoveSpoilerTags(opPost.Comment);
                    topic = ChanTools.ShortenPost(topic, TopicMaxLines, TopicMaxChars, ContinuationSymbol);
                }

                if (string.IsNullOrWhiteSpace(topic))
                    return null;
                else
                    return string.Format("[ /{0}/ - {1} ] [ {2} ]", opPost.Board, opPost.BoardName, topic);
            }
            else
            {
                opPost.HandleError(url);
                return null;
            }
        }
    }


    public class DanboHandler
    {
        public int MaxTagCount { get; set; }
        public string ContinuationSymbol { get; set; }
        public bool Colourize { get; set; }
        public HashSet<string> WarningTags { get; set; }

        string[] codes = {"\u000303", "\u000306", "\u000305"};
        public string CharacterCode
        {
            get { return codes[0]; }
            set { codes[0] = value; }
        }
        public string CopyrightCode
        {
            get { return codes[1]; }
            set { codes[1] = value; }
        }
        public string ArtistCode
        {
            get { return codes[2]; }
            set { codes[2] = value; }
        }

        string _normalCode = "";
        public string NormalCode
        {
            get { return _normalCode; }
            set { _normalCode = value; }
        }

        const string resetCode = "\u000F";


        public string PostToIrc(string url)
        {
            DanboPost postInfo = DanboTools.GetPostInfo(url);

            if (postInfo.Success)
            {
                string rating = ResolveRating(postInfo.Rated);
                string warning = ConstructWarning(postInfo.GeneralTags);

                // If image has no character, copyright or artist tags, return just the post ID and rating.
                if (postInfo.CopyrightTags.Length == 0 &&
                    postInfo.CharacterTags.Length == 0 &&
                    postInfo.ArtistTags.Length == 0)
                {
                    return string.Format("{0}[{1}] [ #{2} ] {3}", NormalCode, rating, postInfo.PostNo, warning);
                }

                string[] cleanedCharacters =
                    DanboTools.CleanupCharacterTags(postInfo.CharacterTags, postInfo.CopyrightTags);

                // Convert to string and limit the number of tags as specified in `MaxTagCount`.
                var characters = TagArrayToString(cleanedCharacters);
                var copyrights = TagArrayToString(postInfo.CopyrightTags);
                var artists = TagArrayToString(postInfo.ArtistTags);
                // Colourize the tags.
                if (Colourize)
                {
                    characters = ColourizeTags(characters, CharacterCode);
                    copyrights = ColourizeTags(copyrights, CopyrightCode);
                    artists = ColourizeTags(artists, ArtistCode);
                }
                
                string danbo = FormatDanboInfo(characters, copyrights, artists);
                
                return string.Format("{0}[{1}] [ {2} ] {3}", NormalCode, rating, danbo, warning);
            }
            else
            {
                postInfo.HandleError(url);
                return null;
            }
        }

        static string ResolveRating(DanboPost.Rating rating)
        {
            if (rating == DanboPost.Rating.Safe)
                return "s";
            else if (rating == DanboPost.Rating.Questionable)
                return "q";
            else
                return "e";
        }


        string ConstructWarning(string[] generalTags)
        {
            // Return early if there's nothing to do.
            if (WarningTags == null || WarningTags.Count == 0 || generalTags.Length == 0)
                return string.Empty;

            var warnings = new List<string>();
            foreach (string tag in generalTags)
                if (WarningTags.Contains(tag))
                    warnings.Add(tag);

            if (warnings.Count > 0)
                return string.Format( "[Warning: {0}]", string.Join(", ", warnings) );
            else
                return string.Empty;
        }


        string TagArrayToString(string[] tags)
        {
            if (tags.Length > MaxTagCount)
                return string.Concat( string.Join(" ", tags, 0, MaxTagCount), ContinuationSymbol );
            else
                return string.Join(" ", tags);
        }
        
        
        string ColourizeTags(string tags, string colour)
        {
            if (string.IsNullOrEmpty(tags))
                return tags;
            else
                return string.Concat(colour, tags, resetCode, NormalCode);
        }


        static string FormatDanboInfo(string characters, string copyrights, string artists)
        {
            string danbo = "";
            
            // If we have characters and copyrights, use them both. If we just have either characters or copyrights
            // use the one we have.
            if (!string.IsNullOrEmpty(characters))
            {                
                if (!string.IsNullOrEmpty(copyrights))
                    danbo = string.Format("{0} ({1})", characters, copyrights);
                else
                    danbo = characters;
            }
            else if (!string.IsNullOrEmpty(copyrights))
                danbo = copyrights;
            
            // Use the artists tags if we have them.
            if (!string.IsNullOrEmpty(artists))
            {
                // Dependent on whether we have the previous 2 (characters and copyrights) prepend the artist bit with
                // a space.
                if (danbo == "")
                    danbo = string.Concat("drawn by ", artists);
                else
                    danbo = string.Concat(danbo, " drawn by ", artists);
            }
            
            return danbo;
        }
    }
}