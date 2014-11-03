using System;
using IvionWebSoft;


namespace WebIrc
{
    public class ChanHandler
    {
        public int TopicMaxLines { get; set; }
        public int TopicMaxChars { get; set; }
        public string ContinuationSymbol { get; set; }
        
        public TitlingResult ThreadTopicToIrc(TitlingRequest req)
        {
            ChanPost opPost = ChanTools.GetThreadOP(req.Url);
            req.Resource = opPost;
            
            if (opPost.Success)
            {
                string topic = null;
                // Prefer subject as topic, if the post has one. Else reform the message into a topic.
                // If a post has neither subject or comment/message, return null.
                if (!string.IsNullOrEmpty(opPost.Subject))
                    topic = opPost.Subject;
                else if (!string.IsNullOrEmpty(opPost.Comment))
                {
                    topic = ChanTools.RemoveSpoilerTags(opPost.Comment);
                    topic = ShortenPost(topic);
                }
                
                if (string.IsNullOrWhiteSpace(topic))
                {
                    req.AddMessage("First post contained neither subject or comment.");
                    return req.CreateResult(false);
                }
                else
                {
                    req.ConstructedTitle = string.Format("[ /{0}/ - {1} ] [ {2} ]",
                                                         opPost.Board, opPost.BoardName, topic);
                    return req.CreateResult(true);
                }
            }
            else
                return req.CreateResult(false);
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