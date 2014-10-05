using System;
using System.Text;
using System.Collections.Generic;
// For dealing with SSL/TLS.
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
// My personal help for dealing with the pesky world of HTTP/HTML and the idiots that misuse it.
using IvionWebSoft;


namespace WebIrc
{
    /// <summary>
    /// Takes an URL and returns an IRC-printable title.
    /// This class is NOT threadsafe.
    /// </summary>
    public class WebToIrc
    {
        public double Threshold { get; set; }
        public bool ParseMedia { get; set; }

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


        public RequestResult GetWebInfo(string url)
        {
            var request = new RequestObject(url);

            // Danbooru handling.
            if (url.Contains("donmai.us/posts/", StringComparison.OrdinalIgnoreCase))
            {
                return Danbo.PostToIrc(request);
            }
            // Gelbooru handling.
            else if (url.Contains("gelbooru.com/index.php?page=post&s=view&id=", StringComparison.OrdinalIgnoreCase))
            {
                return Gelbo.PostToIrc(request);
            }
            // Foolz and 4chan handling.
            else if (ChanTools.IsAddressSupported(url))
            {
                return Chan.ThreadTopicToIrc(request);
            }


            // ----- Above: Don't Need HTML and/or Title -----
            // -----------------------------------------------
            string htmlContent = GetHtmlContent(request);
            if (htmlContent == null)
            {
                // Binary/media handling.
                if (ParseMedia && request.Resource.Exception is UrlNotHtmlException)
                {
                    return BinaryHandler.MediaToIrc(request);
                }

                request.AddMessage("Not (X)HTML: " + request.Url);
                return request.CreateResult(false);
            }

            string htmlTitle = WebTools.GetTitle(htmlContent);
            if (string.IsNullOrWhiteSpace(htmlTitle))
            {
                request.AddMessage("No <title> found, or title element was empty/whitespace.");
                return request.CreateResult(false);
            }

            request.ConstructedTitle = string.Format("[ {0} ]", htmlTitle);
            // -----------------------------------------
            // ----- Below: Need HTML and/or Title -----


            // Youtube handling.
            if (htmlTitle.EndsWith("- YouTube", StringComparison.Ordinal))
            {
                // If duration can be found, change the html info to include that.
                int ytTime = WebTools.GetYoutubeTime(htmlContent);
                if (ytTime > 0)
                {
                    int ytMinutes = ytTime / 60;
                    int ytSeconds = ytTime % 60;
                    request.ConstructedTitle = string.Format("[ {0} ] [{1}:{2:00}]",
                                                             htmlTitle, ytMinutes, ytSeconds);
                }
                return request.CreateResult(true);
            }
            // Other URLs.
            else
                return GenericHandler(request, htmlTitle);
        }


        RequestResult GenericHandler(RequestObject req, string htmlTitle)
        {
            // Because the similarity can only be 1 max, allow all titles to be printed if Threshold is set to 1 or
            // higher. The similarity would always be equal to or less than 1.
            if (Threshold >= 1)
                return req.CreateResult(true);
            // If Threshold is set to 0 that would still mean that titles that had 0 similarity with their URLs would
            // get printed. Set to a negative value to never print any title.
            else if (Threshold < 0)
                return req.CreateResult(false);

            double urlTitleSimilarity = urlTitleComp.CompareUrlAndTitle(req.Url, htmlTitle);
            req.AddMessage("URL-Title Similarity: " + urlTitleSimilarity);

            if (urlTitleSimilarity <= Threshold)
                return req.CreateResult(true);
            else
                return req.CreateResult(false);
        }


        string GetHtmlContent(RequestObject req)
        {
            var webStr = htmlEncHelper.GetWebString(req.Url, Cookies);

            // Mono/.NET can be very strict concerning cookies.
            if (!webStr.Success && webStr.Exception.InnerException is CookieException)
            {
                webStr = htmlEncHelper.GetWebString(req.Url);
                req.AddMessage("CookieException was caught! Page requested without cookies.");
            }

            req.Resource = webStr;

            if (webStr.Success)
            {
                var encInfo = string.Format("(HTTP) \"{0}\" -> {1} ; (HTML) \"{2}\" -> {3}",
                                            htmlEncHelper.HeadersCharset, htmlEncHelper.EncHeaders,
                                            htmlEncHelper.HtmlCharset, htmlEncHelper.EncHtml);
                req.AddMessage(encInfo);

                return webStr.Document;
            }
            else
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
    }
}