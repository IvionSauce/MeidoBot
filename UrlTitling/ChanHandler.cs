using System;
using IvionWebSoft;


namespace WebIrc
{
    public class ChanHandler
    {
        public int TopicMaxLines { get; set; }
        public int TopicMaxChars { get; set; }
        public string ContinuationSymbol { get; set; }

        private enum Source
        {
            FourChan,
            ArchiveMoe
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
                string topic = null;

                // Prefer subject as topic, if the post has one and if it isn't too similar to the URL. This is now an
                // issue because 4chan puts the subject into the URL.
                if (!string.IsNullOrEmpty(post.Subject))
                {
                    double similarity = WebToIrc.UrlTitle.Similarity(req.Url, post.Subject);
                    if (similarity < 0.9d)
                        topic = post.Subject;
                }

                // Else reform the message into a topic.
                if (topic == null && !string.IsNullOrEmpty(post.Comment))
                {
                    topic = ChanTools.RemoveSpoilerTags(post.Comment);
                    topic = ChanTools.RemovePostQuotations(topic);
                    topic = ShortenPost(topic);
                }
                
                if (topic == null)
                {
                    req.AddMessage("Post contained neither subject or comment.");
                    return req.CreateResult(false);
                }

                req.ConstructedTitle = string.Format("[ /{0}/ - {1} ] [ {2} ]",
                                                     post.Board, post.BoardName, topic);
                return req.CreateResult(true);
            }
            else
                return req.CreateResult(false);
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
            else if (url.Contains("archive.moe/", StringComparison.OrdinalIgnoreCase) &&
                     !url.Contains("/image/", StringComparison.OrdinalIgnoreCase))
                return Source.ArchiveMoe;
            else
                return null;
        }


        // Shorten post by limiting the amount of lines/sentences. Also check if post is below max amount of characters,
        // even when it has had its lines reduced. The continuation symbol is appended if the post was shortened. Either
        // of the limits (lines and chars) can be disabled by having them be <= 0.
        // Newlines are replaced with spaces and multiple, consecutive newlines are squashed.
        string ShortenPost(string post)
        {            
            bool shortenLines = TopicMaxLines > 0;
            bool shortenChars = TopicMaxChars > 0;
            
            string[] postLines = post.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            
            string shortPost;
            if (shortenLines && postLines.Length > TopicMaxLines)
                shortPost = string.Join(" ", postLines, 0, TopicMaxLines);
            else
                shortPost = string.Join(" ", postLines);
            
            if (shortenChars && shortPost.Length > TopicMaxChars)
            {
                shortPost = shortPost.Substring(0, TopicMaxChars);
                return string.Concat(shortPost, ContinuationSymbol);
            }
            else if (shortenLines && postLines.Length > TopicMaxLines)
                return string.Concat(shortPost, " ", ContinuationSymbol);
            else
                return shortPost;
        }
    }
}