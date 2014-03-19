using System;
using System.Text;
using System.Collections.Generic;
// For dealing with SSL/TLS.
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
// My personal help for dealing with the pesky world of HTTP/HTML and the idiots that misuse it.
using HtmlHelp;
// Various website utilities, both generic and specific.
using WebToolsModule;

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
            else if (url.Contains("boards.4chan.org/", StringComparison.OrdinalIgnoreCase) ||
                     url.Contains("archive.foolz.us/", StringComparison.OrdinalIgnoreCase))
            {
                return Chan.ThreadTopicToIrc(url);
            }


            // ----- Above: Don't Need HTML and/or Title -----
            // -----------------------------------------------
            string htmlString = GetHtmlString(url);
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
            else if (url.Contains("youtube.com/watch?", StringComparison.OrdinalIgnoreCase) || 
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
                double urlAndTitleSimilarity = urlTitleComp.CompareUrlAndTitle(url, htmlTitle);

                // Debug
                Console.WriteLine("URL-Title Similarity: " + urlAndTitleSimilarity);

                // If the URL and Title are too similar, don't return HTML info for printing to IRC.
                if (urlAndTitleSimilarity > Threshold)
                    return null;

                return string.Format("[ {0} ]", htmlTitle);
            }
        }

        string GetHtmlString(string url)
        {
            // State we want to use our ACCEPT ALL implementation.
            ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificates;

            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (UriFormatException)
            {
                Console.WriteLine("--- UriFormatException, URL was malformed.");
                return null;
            }

            try
            {
                htmlEncHelper.Load(uri, Cookies);
            }
            catch (WebException webex)
            {
                // Some ugly stuff to work around .NET being very strict about cookies and their domains.
                if (webex.InnerException is CookieException)
                {
                    Console.WriteLine("--- CookieException caught! Trying without cookies.");
                    try
                    {
                        htmlEncHelper.Load(uri);
                    }
                    catch (WebException webex2)
                    {
                        Console.WriteLine("--- WebException at htmlEncHelper.Load(url): " + webex2.Message);
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("--- WebException at htmlEncHelper.Load(url): " + webex.Message);
                    return null;
                }
            }

            string htmlString = htmlEncHelper.GetHtmlAsString();
            if (htmlString == null)
            {
                Console.WriteLine("Non-HTML: " + url);
                return null;
            }
            else
            {
                Console.WriteLine("(HTTP) {0} -> {1} ; (HTML) {2} -> {3}",
                                  htmlEncHelper.HeadersCharset, htmlEncHelper.EncHeaders,
                                  htmlEncHelper.HtmlCharset, htmlEncHelper.EncHtml);
                return htmlString;
            }
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

            if (opPost == null)
                return null;

            string topic;
            // Prefer subject as topic, if the post has one. Else reform the message into a topic.
            // If a post has neither subject or comment/message, return null.
            if (opPost.Subject != null)
                topic = opPost.Subject;
            else if (opPost.Comment != null)
            {
                topic = ChanTools.RemoveSpoilerTags(opPost.Comment);
                topic = ChanTools.ShortenPost(topic, TopicMaxLines, TopicMaxChars, ContinuationSymbol);
            }
            else
                return null;

            return string.Format("[ /{0}/ - {1} ] [ {2} ]", opPost.Board, opPost.BoardName, topic);
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
            Dictionary<string, string> postInfo = DanboTools.GetPostInfo(url);

            if (postInfo == null)
                return null;

            string[] postArr = {postInfo["characters"], postInfo["copyrights"], postInfo["artists"]};

            // If image had no character, copyright or artist tags, return just the post ID.
            if (string.IsNullOrEmpty(postArr[0]) &&
                string.IsNullOrEmpty(postArr[1]) &&
                string.IsNullOrEmpty(postArr[2]))
                return string.Format("{0}[ #{1} ]", NormalCode, postInfo["id"]);

            // Shorten and colourize the tags.
            for (int i = 0; i < postArr.Length; i++)
            {
                postArr[i] = DanboTools.ShortenTagList(postArr[i], MaxTagCount, ContinuationSymbol);
                if (Colourize)
                    postArr[i] = ColourizeTags(postArr[i], codes[i]);
            }

            string danbo = FormatDanboInfo(postArr[0], postArr[1], postArr[2]);

            return string.Format("{0}[ {1} ]", NormalCode, danbo);
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


static class ExtensionMethods
{
    public static bool Contains(this string source, string value, StringComparison comp)
    {
        return source.IndexOf(value, comp) >= 0;
    }
}