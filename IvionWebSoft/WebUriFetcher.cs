using System;
using System.Net;


namespace IvionWebSoft
{
    public enum UriLimit
    {
        None,
        SameHost,
        SameScheme
    }


    public class WebUriFetcher
    {
        public int MaxSizeHtml { get; set; }
        public int MaxSizeNonHtml { get; set; }

        public int Timeout { get; set; }
        public string UserAgent { get; set; }
        public CookieContainer Cookies { get; set; }

        const int MaxRefreshes = 10;
        public bool FollowMetaRefreshes { get; set; }
        UriLimit MetaRefreshLimitation { get; set; }


        public WebUriFetcher()
        {
            // Default maximum is 1MB.
            MaxSizeHtml = 1048576;
            // Don't download non-HTML by default
            MaxSizeNonHtml = 0;

            // Default is 30 seconds.
            Timeout = 30000;
            UserAgent = "Mozilla/5.0 IvionWebSoft/1.0";
            // By default limit Refresh URI's to the same scheme as the request.
            MetaRefreshLimitation = UriLimit.SameScheme;
        }


        public FetcherResult Load(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            // Use `int.MaxValue` as a special value (don't follow redirects).
            // Really, any number larger than `MaxRefreshes` would do.
            return Load(
                uri, true,
                FollowMetaRefreshes ? 0 : int.MaxValue,
                null
            );
        }

        FetcherResult Load(Uri uri, bool useCookies, int redirects, string previousPage)
        {
            var req = SetupRequest(uri, useCookies);
            var wb = WebBytes.Create(req, MaxSizeHtml, MaxSizeNonHtml);

            if (wb.ContentIsHtml)
            {
                var page = WebString.Create(wb, EncHelp.Windows1252);

                Uri refreshUrl;
                if (redirects < MaxRefreshes &&
                    TryGetMetaRefresh(page, out refreshUrl) &&
                    VerifyRefresh(page.Location, refreshUrl))
                {
                    // If during the redirects we get a different page/HTML string, refrain from following more
                    // redirects. It probably means we've arrived, but only more real world testing will tell us
                    // if that's true.
                    // Otherwise keep trying to follow the redirects.
                    if (redirects == 0 || page.Document == previousPage)
                        return Load(refreshUrl, useCookies, (redirects + 1), page.Document);
                }

                return new FetcherResult(wb, page);
            }
            // Silently handle cookie exceptions, Mono/.NET can be very strict with which cookies it accepts.
            if (!wb.Success && wb.Exception.InnerException is CookieException)
                return Load(uri, false, redirects, previousPage);

            // If either not HTML or there was an error in getting the resource, return as-is.
            return new FetcherResult(wb);
        }

        WebRequest SetupRequest(Uri url, bool useCookies)
        {
            WebRequest req = WebRequest.Create(url);
            req.Timeout = Timeout;

            var httpReq = req as HttpWebRequest;
            if (httpReq != null)
            {
                httpReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                httpReq.Accept = "*/*";

                httpReq.UserAgent = UserAgent;
                // Some sites use a combination of Meta Refresh and cookies in redirecting you to their actual page.
                if (useCookies)
                    httpReq.CookieContainer = Cookies;
            }

            return req;
        }


        static bool TryGetMetaRefresh(WebString page, out Uri refreshUrl)
        {
            string metaRefresh = HtmlTagExtract.GetMetaRefresh(page.Document);
            if (metaRefresh != null)
            {
                // Relative refresh URL.
                // Try this one first, because creating an absolute URL out of a relative one starting with a '/' can
                // lead to it being interpreted as a file:/// URI.
                if (Uri.TryCreate(page.Location, metaRefresh, out refreshUrl))
                    return true;
                // Absolute refresh URL.
                if (Uri.TryCreate(metaRefresh, UriKind.Absolute, out refreshUrl))
                    return true;
            }

            refreshUrl = null;
            return false;
        }


        bool VerifyRefresh(Uri current, Uri refresh)
        {
            switch (MetaRefreshLimitation)
            {
            case UriLimit.None:
                return true;

            case UriLimit.SameHost:
                if (refresh.Host == current.Host)
                    return true;
                break;

            case UriLimit.SameScheme:
                if (refresh.Scheme == current.Scheme)
                    return true;
                break;
            }

            return false;
        }
    }


    public class FetcherResult
    {
        public WebBytes Bytes { get; private set; }
        public HtmlPage Page { get; private set; }

        public bool IsHtml
        {
            get { return Bytes.ContentIsHtml; }
        }


        internal FetcherResult(WebBytes bytes, WebString page)
        {
            Bytes = bytes;
            Page = new HtmlPage(bytes, page);
        }

        public FetcherResult(WebBytes bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            Bytes = bytes;
            Page = HtmlPage.Create(bytes);
        }
    }
}