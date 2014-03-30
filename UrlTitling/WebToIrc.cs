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


        public static void ReportError(this WebResource resource, string url)
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
}