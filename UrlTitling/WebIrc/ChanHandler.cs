using System;
using IvionWebSoft;
using MeidoCommon.Formatting;


namespace WebIrc
{
    public class ChanHandler
    {
        public int TopicMaxLines { get; set; }
        public int TopicMaxChars { get; set; }
        public string ContinuationSymbol { get; set; }

        enum Source
        {
            FourChan,
            ArchiveMoe
        }


        public TitlingResult HandleRequest(TitlingRequest req)
        {
            if (Supports(req))
                return ThreadTopicToIrc(req);

            return null;
        }

        public TitlingResult ThreadTopicToIrc(TitlingRequest req)
        {
            ChanPost post;
            switch (GetSource(req.Url))
            {
            case Source.FourChan:
                post = FourChan.GetPost(req.Url);
                break;
            case Source.ArchiveMoe:
                post = ArchiveMoe.GetPost(req.Url);
                break;
            default:
                // Fail loudly for now, this might change in the future.
                throw new NotSupportedException("Passed TitlingRequest not supported.");
            }
            req.Resource = post;
            
            if (post.Success)
            {
                string topic = ConstructTopic(post, req.Url);
                if (topic == null)
                {
                    req.AddMessage("Post contained neither subject or comment.");
                    return req.CreateResult(false);
                }

                req.IrcTitle.SetFormat("[ /{0}/ - {1} ] [ {2} ]", post.Board, post.BoardName, topic);
                return req.CreateResult(true);
            }
            else
                return req.CreateResult(false);
        }

        string ConstructTopic(ChanPost post, string url)
        {
            // Prefer subject as topic, if the post has one and if it isn't too similar to the URL. This is now an
            // issue because 4chan puts the subject into the URL.
            if (!string.IsNullOrEmpty(post.Subject))
            {
                double similarity = WebToIrc.UrlTitle.Similarity(url, post.Subject);
                if (similarity < 0.9d)
                    return post.Subject;
            }

            // Else reform the message into a topic.
            if (!string.IsNullOrEmpty(post.Comment))
            {
                string topic = ChanTools.RemoveSpoilerTags(post.Comment);
                topic = ChanTools.RemovePostQuotations(topic);
                return ShortenPost(topic);
            }

            return null;
        }


        public static bool Supports(TitlingRequest req)
        {
            Source? src = GetSource(req.Url);
            if (src == null)
                return false;
            else
                return true;
        }

        static Source? GetSource(string url)
        {
            if (url.Contains("boards.4chan.org/", StringComparison.OrdinalIgnoreCase))
                return Source.FourChan;
            // Guard against URL's pointing to an image.
            if (url.Contains("archive.moe/", StringComparison.OrdinalIgnoreCase) &&
                !url.Contains("/image/", StringComparison.OrdinalIgnoreCase))
                return Source.ArchiveMoe;

            return null;
        }


        // Newlines are replaced with spaces and multiple, consecutive newlines are squashed.
        string ShortenPost(string post)
        {
            string[] postLines = post.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            return Format.Shorten(postLines, TopicMaxLines, TopicMaxChars, ContinuationSymbol);
        }
    }
}