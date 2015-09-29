using System;
using System.Net;
using IvionWebSoft;


namespace WebIrc
{
    /// <summary>
    /// Takes an URL and returns an IRC-printable title.
    /// </summary>
    public class WebToIrc
    {
        public double Threshold { get; set; }
        public bool ParseMedia { get; set; }

        public ChanHandler Chan { get; private set; }
        public DanboHandler Danbo { get; private set; }
        public GelboHandler Gelbo { get; private set; }
        public WikipediaHandler Wiki { get; private set; }

        public CookieContainer Cookies
        {
            get { return urlFollower.Cookies; }
        }


        internal static UrlTitleComparer UrlTitle { get; private set; }

        readonly MetaRefreshFollower urlFollower = new MetaRefreshFollower();


        static WebToIrc()
        {
            UrlTitle = new UrlTitleComparer();
        }
        public WebToIrc()
        {
            Chan = new ChanHandler();
            Danbo = new DanboHandler();
            Gelbo = new GelboHandler();
            Wiki = new WikipediaHandler();

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
                return MiscHandlers.BinaryToIrc(request, result.Bytes);
            }
            else
            {
                return request.CreateResult(false);
            }
        }


        TitlingResult HandleHtml(TitlingRequest request, HtmlPage page)
        {
            const int maxTitleLength = 1024;

            ReportCharsets(request, page);

            string htmlTitle = WebTools.GetTitle(page.Content);
            if (string.IsNullOrWhiteSpace(htmlTitle))
            {
                request.AddMessage("No <title> found, or title element was empty/whitespace.");
                return request.CreateResult(false);
            }
            else if (htmlTitle.Length > maxTitleLength)
            {
                request.AddMessage("HTML title length was in excess of 1024 characters, assuming spam.");
                return request.CreateResult(false);
            }
            // If defined and not of ridiculous length make it available to ConstructedTitle.
            request.ConstructedTitle.HtmlTitle = htmlTitle;

            // Youtube handling.
            if (htmlTitle.EndsWith("- YouTube", StringComparison.Ordinal))
            {
                return MiscHandlers.YoutubeWithDuration(request, page.Content);
            }
            // Wikipedia handling.
            else if (request.Url.Contains("wikipedia.org/", StringComparison.OrdinalIgnoreCase))
            {
                return Wiki.WikipediaSummarize(request, page.Content);
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
            req.AddMessage(
                string.Format("URL-Title Similarity: {0} [Threshold: {1}]", urlTitleSimilarity, Threshold));

            if (urlTitleSimilarity <= Threshold)
                return req.CreateResult(true);
            else
                return req.CreateResult(false);
        }
    }
}