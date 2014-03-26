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
        HtmlEncodingHelper htmlEncHelper = new HtmlEncodingHelper();
        UrlTitleComparer urlTitleComp = new UrlTitleComparer();

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
                Console.WriteLine("Non-(X)HTML: " + url);
            else
                Console.WriteLine("--- Error getting {0} ({1})", url, webStr.Exception.Message);

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


    internal static class ExtensionMethods
    {
        internal static bool Contains(this string source, string value, StringComparison comp)
        {
            return source.IndexOf(value, comp) >= 0;
        }
    }


    public class ChanHandler
    {
        int _maxLines = 2;
        int _maxChars = 128;
        public int TopicMaxLines
        {
            get { return _maxLines; }
            set { _maxLines = value; }
        }
        public int TopicMaxChars
        {
            get { return _maxChars; }
            set { _maxChars = value; }
        }
        string _cont = "…";
        public string ContinuationSymbol
        {
            get { return _cont; }
            set { _cont = value; }
        }

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
                string message = "Unknown error, contact software author.";
                if (opPost.Exception != null)
                    message = opPost.Exception.Message;
                
                Console.WriteLine("--- Error getting {0} ({1})", url, message);
                return null;
            }
        }
    }


    public class DanboHandler
    {
        int _maxTagCount = 5;
        public int MaxTagCount
        {
            get { return _maxTagCount; }
            set { _maxTagCount = value; }
        }

        string _cont = "[...]";
        public string ContinuationSymbol
        {
            get { return _cont; }
            set { _cont = value; }
        }

        bool _colourize = true;
        public bool Colourize
        {
            get { return _colourize; }
            set { _colourize = value; }
        }

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

                // If image has no character, copyright or artist tags, return just the post ID and rating.
                if (postInfo.CopyrightTags.Length == 0 &&
                    postInfo.CharacterTags.Length == 0 &&
                    postInfo.ArtistTags.Length == 0)
                {
                    return string.Format("{0}[{1}] [ #{2} ]", NormalCode, rating, postInfo.PostNo);
                }

                // Convert to string and limit the number of tags as specified in `MaxTagCount`.
                var characters =
                    DanboTools.TagArrayToString( postInfo.CharacterTags, MaxTagCount, ContinuationSymbol );
                var copyrights =
                    DanboTools.TagArrayToString( postInfo.CopyrightTags, MaxTagCount, ContinuationSymbol );
                var artists =
                    DanboTools.TagArrayToString( postInfo.ArtistTags, MaxTagCount, ContinuationSymbol );
                // Colourize the tags.
                if (Colourize)
                {
                    characters = ColourizeTags(characters, CharacterCode);
                    copyrights = ColourizeTags(copyrights, CopyrightCode);
                    artists = ColourizeTags(artists, ArtistCode);
                }
                
                string danbo = FormatDanboInfo(characters, copyrights, artists);
                
                return string.Format("{0}[{1}] [ {2} ]", NormalCode, rating, danbo);
            }
            else
            {
                string message = "Unknown error, contact software author.";
                if (postInfo.Exception != null)
                    message = postInfo.Exception.Message;
                
                Console.WriteLine("--- Error getting {0} ({1})", url, message);
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

        static string FormatDanboInfo(string characters, string copyrights, string artists)
        {
            string danbo = "";

            // If we have characters and copyrights, use them both. If we just have either characters or copyrights
            // use the one we have.
            if (!string.IsNullOrEmpty(characters))
            {
                string cleanedupCharacters = DanboTools.CleanupCharacterTags(characters);

                if (!string.IsNullOrEmpty(copyrights))
                    danbo = string.Format("{0} ({1})", cleanedupCharacters, copyrights);
                else
                    danbo = cleanedupCharacters;
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

        string ColourizeTags(string tags, string colour)
        {
            if (string.IsNullOrEmpty(tags))
                return tags;
            else
                return string.Concat(colour, tags, resetCode, NormalCode);
        }
    }
}