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
                if (opPost.Subject != null)
                    topic = opPost.Subject;
                else if (opPost.Comment != null)
                {
                    topic = ChanTools.RemoveSpoilerTags(opPost.Comment);
                    topic = ChanTools.ShortenPost(topic, TopicMaxLines, TopicMaxChars, ContinuationSymbol);
                }
                
                if (string.IsNullOrWhiteSpace(topic))
                    return null;
                else
                    return string.Format("[ /{0}/ - {1} ] [ {2} ]", opPost.Board, opPost.BoardName, topic);
            }
            else
            {
                opPost.HandleError(url);
                return null;
            }
        }
    }
}