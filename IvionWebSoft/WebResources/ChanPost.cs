using System;


namespace IvionWebSoft
{
    public class ChanPost : WebResource
    {
        /// <summary>
        /// Short board designation, without the slashes.
        /// </summary>
        public string Board { get; private set; }
        /// <summary>
        /// Full board name. Will be empty in case board doesn't map to a name.
        /// </summary>
        public string BoardName { get; private set; }
        /// <summary>
        /// Thread number.
        /// </summary>
        public int ThreadNo { get; private set; }
        /// <summary>
        /// Post number.
        /// </summary>
        public int PostNo { get; private set; }
        /// <summary>
        /// Subject of the post. Will be empty if no subject.
        /// </summary>
        public string Subject { get; private set; }
        /// <summary>
        /// Comment/message of the post. Will be empty if no comment.
        /// </summary>
        public string Comment { get; private set; }


        internal ChanPost(WebResource resource) : base(resource) {}

        public ChanPost(Uri uri, Exception ex) : base(uri, ex) {}


        public ChanPost(Uri uri,
            string board, string boardName,
            int threadNo, int postNo,
            string subject, string comment) : base(uri)
        {
            if (board == null)
                throw new ArgumentNullException("board");
            else if (threadNo < 0)
                throw new ArgumentOutOfRangeException("threadNo", "Cannot be 0 or negative.");
            else if (postNo < 0)
                throw new ArgumentOutOfRangeException("postNo", "Cannot be 0 or negative.");

            Board = board;
            BoardName = boardName ?? string.Empty;
            ThreadNo = threadNo;
            PostNo = postNo;
            // Make sure subject and comment are not null.
            Subject = subject ?? string.Empty;
            Comment = comment ?? string.Empty;
        }
    }
}