using System;
using System.Net;


namespace IvionWebSoft
{
    public class MetaRefreshFollower
    {
        public int MaxSizeHtml { get; set; }
        public int FetchSizeNonHtml { get; set; }

        public int Timeout { get; set; }
        public string UserAgent { get; set; }
        public CookieContainer Cookies { get; set; }


        public MetaRefreshFollower()
        {
            // Standard maximum is 1MB.
            MaxSizeHtml = 1048576;
            FetchSizeNonHtml = 0;

            // Standard is 30 seconds.
            Timeout = 30000;
            UserAgent = "Mozilla/5.0 IvionWebSoft/1.0";
        }


        public FollowerResult Load(Uri uri)
        {
            return Load(uri, true, 0, null);
        }

        FollowerResult Load(Uri uri, bool useCookies, int redirects, string previousPage)
        {
            var req = SetupRequest(uri, useCookies);
            var wb = WebBytes.Create(req, MaxSizeHtml, FetchSizeNonHtml);

            if (wb.ContentIsHtml)
            {
                var page = WebString.Create(wb, EncHelp.Windows1252);

                // Only follow a HTML/"Meta Refresh" URL 10 times, we don't want to get stuck in a loop.
                const int MaxRedirects = 10;
                Uri refreshUrl;
                if (redirects < MaxRedirects && TryGetMetaRefresh(page, out refreshUrl))
                {
                    // If during the redirects we get a different page/HTML string, refrain from following more
                    // redirects. It probably means we've arrived, but only more real world testing will tell us
                    // if that's true.
                    // Otherwise keep trying to follow the redirects.
                    if (redirects == 0 || page.Document == previousPage)
                        return Load(refreshUrl, useCookies, (redirects + 1), page.Document);
                }

                return new FollowerResult(wb, page);
            }
            // Silently handle cookie exceptions, Mono/.NET can be very strict with which cookies it accepts.
            else if (!wb.Success && wb.Exception.InnerException is CookieException)
                return Load(uri, false, redirects, previousPage);

            // If either not HTML or there was an error in getting the resource, return as-is.
            else
                return new FollowerResult(wb);
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
                else if (Uri.TryCreate(metaRefresh, UriKind.Absolute, out refreshUrl))
                    return true;
            }

            refreshUrl = null;
            return false;
        }
    }


    public class FollowerResult
    {
        public WebBytes Bytes { get; private set; }
        public HtmlPage Page { get; private set; }

        public bool IsHtml
        {
            get { return Bytes.ContentIsHtml; }
        }


        internal FollowerResult(WebBytes bytes, WebString page)
        {
            Bytes = bytes;
            Page = new HtmlPage(bytes, page);
        }

        public FollowerResult(WebBytes bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");

            Bytes = bytes;
            Page = HtmlPage.Create(bytes);
        }
    }
}