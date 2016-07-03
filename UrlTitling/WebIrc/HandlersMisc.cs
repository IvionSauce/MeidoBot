using System;
using System.Net;
using System.Text;
using IvionWebSoft;


namespace WebIrc
{
    public static class MiscHandlers
    {
        public static TitlingResult YoutubeWithDuration(TitlingRequest req, string htmlDoc)
        {
            // If duration can be found, change the html info to include that.
            var ytTime = WebTools.GetYoutubeTime(htmlDoc);
            req.IrcTitle.SetHtmlTitle().AppendTime(ytTime);

            return req.CreateResult(true);
        }


        public static bool IsTwitter(TitlingRequest req)
        {
            if (req.Uri.Host.Equals("twitter.com", StringComparison.OrdinalIgnoreCase) ||
                req.Uri.Host.Equals("t.co", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static TitlingResult Twitter(TitlingRequest req, CookieContainer cookies)
        {
            var request = WebRequest.Create(req.Uri);
            var httpReq = request as HttpWebRequest;
            if (httpReq != null)
                httpReq.CookieContainer = cookies;
            
            var tweet = WebString.Create(request, Encoding.UTF8);
            if (tweet.Success)
            {
                req.Resource = tweet;
                req.IrcTitle.HtmlTitle = WebTools.GetTitle(tweet.Document);

                return req.CreateResult(true);
            }

            return req.CreateResult(false);
        }
    }
}