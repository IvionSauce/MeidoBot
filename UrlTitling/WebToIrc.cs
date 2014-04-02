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
        public double Threshold { get; set; }

        public ChanHandler Chan { get; private set; }
        public DanboHandler Danbo { get; private set; }
        public GelboHandler Gelbo { get; private set; }

        public static CookieContainer Cookies { get; private set; }

        readonly HtmlEncodingHelper htmlEncHelper = new HtmlEncodingHelper();
        static readonly UrlTitleComparer urlTitleComp = new UrlTitleComparer();


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
            Gelbo = new GelboHandler();
        }


        public string GetWebInfo(string url)
        {
            // Danbooru handling.
            if (url.Contains("donmai.us/posts/", StringComparison.OrdinalIgnoreCase))
            {
                return Danbo.PostToIrc(url);
            }
            // Gelbooru handling.
            else if (url.Contains("gelbooru.com/index.php?page=post&s=view&id=", StringComparison.OrdinalIgnoreCase))
            {
                return Gelbo.PostToIrc(url);
            }
            // Foolz and 4chan handling.
            else if (ChanTools.IsAddressSupported(url))
            {
                return Chan.ThreadTopicToIrc(url);
            }


            // ----- Above: Don't Need HTML and/or Title -----
            // -----------------------------------------------
            string htmlContent = GetHtmlContent(url);
            if (htmlContent == null)
                // Something went wrong in GetHtmlString, which should've already printed the error.
                return null;

            string htmlTitle = WebTools.GetTitle(htmlContent);
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
                int ytTime = WebTools.GetYoutubeTime(htmlContent);
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
                return GenericHandler(url, htmlTitle);
        }


        string GenericHandler(string url, string htmlTitle)
        {
            var formatted = string.Format("[ {0} ]", htmlTitle);
            // Because the similarity can only be 1 max, allow all titles to be printed if Threshold is set to 1 or
            // higher. The similarity would always be equal to or less than 1.
            if (Threshold >= 1)
                return formatted;
            // If Threshold is set to 0 that would still mean that titles that had 0 similarity with their URLs would
            // get printed. Set to a negative value to never print any title.
            else if (Threshold < 0)
                return null;

            double urlTitleSimilarity = urlTitleComp.CompareUrlAndTitle(url, htmlTitle);
            Console.WriteLine("URL-Title Similarity: " + urlTitleSimilarity);

            if (urlTitleSimilarity <= Threshold)
                return formatted;
            else
                return null;
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
                webStr.ReportError(url);

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
            if (resource.Exception == null || resource.Exception.Message == null)
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