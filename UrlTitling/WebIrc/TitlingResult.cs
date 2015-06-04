using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace WebIrc
{
    public class TitlingResult
    {
        public string Requested { get; private set; }
        public Uri Retrieved { get; private set; }

        public bool Success { get; private set; }
        public Exception Exception { get; private set; }

        public string Title { get; private set; }
        public bool PrintTitle { get; private set; }
        public ReadOnlyCollection<string> Messages { get; private set; }


        public TitlingResult(string requested, Uri retrieved,
            bool success, Exception ex,
            string title, bool printTitle,
            IList<string> messages)
        {
            if (requested == null)
                throw new ArgumentNullException("requested");
            else if (title == null)
                throw new ArgumentNullException("title");
            else if (messages == null)
                throw new ArgumentNullException("messages");

            if (success)
            {
                if (retrieved == null)
                    throw new ArgumentException("Retrieved is null while success is true.");
                else if (ex != null)
                    throw new ArgumentException("Exception is not-null while success is true.");
            }
            else if (ex == null)
                throw new ArgumentException("Must have an exception if success is false.");


            Requested = requested;
            Retrieved = retrieved;
            Success = success;
            Exception = ex;
            Title = title;
            PrintTitle = printTitle;
            Messages = new ReadOnlyCollection<string>(messages);
        }


        public static TitlingResult Failure(string requested, Exception ex)
        {
            return new TitlingResult(requested, null,
                false, ex,
                string.Empty, false,
                new string[0]);
        }
    }
}