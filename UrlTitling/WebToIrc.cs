using System;
using System.Text;
using System.Collections.Generic;
// For dealing with SSL/TLS.
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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

        public CookieContainer Cookies
        {
            get { return urlFollower.Cookies; }
        }


        internal static UrlTitleComparer UrlTitle { get; private set; }

        readonly MetaRefreshFollower urlFollower = new MetaRefreshFollower();


        static WebToIrc()
        {
            // State we want to use our ACCEPT ALL implementation.
            ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificates;
            UrlTitle = new UrlTitleComparer();
        }
        public WebToIrc()
        {
            Chan = new ChanHandler();
            Danbo = new DanboHandler();
            Gelbo = new GelboHandler();

            urlFollower.Cookies = new CookieContainer();
            urlFollower.FetchSizeNonHtml = 64*1024;
        }


        public TitlingResult WebInfo(string uriString)
        {
            if (uriString == null)
                throw new ArgumentNullException("uriString");

            Uri uri;
            try
            {
                uri = new Uri(uriString);
            }
            catch (UriFormatException ex)
            {
                return TitlingResult.Failure(uriString, ex);
            }
            return WebInfo(uri);
        }


        public TitlingResult WebInfo(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            else if (!uri.IsAbsoluteUri)
                throw new ArgumentException("Uri must be absolute: " + uri);

            if (TitlingRequest.IsSchemeSupported(uri))
                return WebInfo( new TitlingRequest(uri) );
            else
            {
                var ex = new NotSupportedException("Unsupported scheme: " + uri.Scheme);
                return TitlingResult.Failure(uri.OriginalString, ex);
            }
        }


        public TitlingResult WebInfo(TitlingRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");
            // TitlingRequest ensures that what we get passed is an absolute URI with a scheme we support. Most
            // importantly this relieves the individual handlers of checking for those conditions.


            // Danbooru handling.
            if (request.Url.Contains("donmai.us/posts/", StringComparison.OrdinalIgnoreCase))
            {
                return Danbo.PostToIrc(request);
            }
            // Gelbooru handling.
            else if (request.Url.Contains("gelbooru.com/index.php?page=post&s=view&id=",
                                          StringComparison.OrdinalIgnoreCase))
            {
                return Gelbo.PostToIrc(request);
            }
            // Foolz and 4chan handling.
            else if (ChanHandler.Supports(request))
            {
                return Chan.ThreadTopicToIrc(request);
            }

            var result = urlFollower.Load(request.Uri);
            request.Resource = result.Page;

            if (result.IsHtml)
            {
                return HandleHtml(request, result.Page);
            }
            else if (ParseMedia && result.Bytes.Success)
            {
                return BinaryHandler.BinaryToIrc(request, result.Bytes);
            }
            else
            {
                return request.CreateResult(false);
            }
        }


        TitlingResult HandleHtml(TitlingRequest request, HtmlPage page)
        {
            ReportCharsets(request, page);

            string htmlTitle = WebTools.GetTitle(page.Content);
            if (string.IsNullOrWhiteSpace(htmlTitle))
            {
                request.AddMessage("No <title> found, or title element was empty/whitespace.");
                return request.CreateResult(false);
            }
            request.ConstructedTitle.HtmlTitle = htmlTitle;

            // Youtube handling.
            if (htmlTitle.EndsWith("- YouTube", StringComparison.Ordinal))
            {
                return MiscHandlers.YoutubeWithDuration(request, page.Content);
            }
            // Wikipedia handling.
            else if (request.Url.Contains("wikipedia.org/", StringComparison.OrdinalIgnoreCase))
            {
                return MiscHandlers.WikipediaSummarize(request, page.Content);
            }
            // Other URLs.
            else
                return GenericHandler(request);
        }

        static void ReportCharsets(TitlingRequest req, HtmlPage page)
        {
            var encInfo = string.Format("(HTTP) \"{0}\" -> {1} ; (HTML) \"{2}\" -> {3}",
                page.HeadersCharset, page.EncHeaders,
                page.HtmlCharset, page.EncHtml);

            req.AddMessage(encInfo);
        }


        TitlingResult GenericHandler(TitlingRequest req)
        {
            // Because the similarity can only be 1 max, allow all titles to be printed if Threshold is set to 1 or
            // higher. The similarity would always be equal to or less than 1.
            if (Threshold >= 1)
                return req.CreateResult(true);
            // If Threshold is set to 0 that would still mean that titles that had 0 similarity with their URLs would
            // get printed. Set to a negative value to never print any title.
            else if (Threshold < 0)
                return req.CreateResult(false);

            double urlTitleSimilarity = UrlTitle.Similarity(req.Url, req.ConstructedTitle.HtmlTitle);
            req.AddMessage("URL-Title Similarity: " + urlTitleSimilarity);

            if (urlTitleSimilarity <= Threshold)
                return req.CreateResult(true);
            else
                return req.CreateResult(false);
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