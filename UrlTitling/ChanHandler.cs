using System;
using WebHelp;


namespace WebIrc
{
    public class ChanHandler
    {
        public int TopicMaxLines { get; set; }
        public int TopicMaxChars { get; set; }
        public string ContinuationSymbol { get; set; }
        
        public string ThreadTopicToIrc(string url)
        {
            ChanPost opPost = ChanTools.GetThreadOP(url);
            
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
                    return null;
                else
                    return string.Format("[ /{0}/ - {1} ] [ {2} ]", opPost.Board, opPost.BoardName, topic);
            }
            else
            {
                opPost.ReportError(url);
                return null;
            }
        }


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