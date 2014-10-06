using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IvionWebSoft;


namespace WebIrc
{
    public class RequestResult
    {
        public string Requested { get; private set; }
        public Uri Retrieved { get; private set; }

        public bool Success { get; private set; }
        public Exception Exception { get; private set; }

        public string Title { get; private set; }
        public bool PrintTitle { get; private set; }
        public ReadOnlyCollection<string> Messages { get; private set; }


        public RequestResult(string requested, Uri retrieved,
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


        public string ReportWebError()
        {
            const string errorMsg = "Error getting {0} ({1})";
            if (Retrieved == null)
                return string.Format(errorMsg, Requested, Exception.Message);
            else
                return string.Format(errorMsg, Retrieved, Exception.Message);
        }
    }


    public class RequestObject
    {
        public readonly Uri Uri;
        public readonly string Url;

        public WebResource Resource { get; set; }

        string _title = string.Empty;
        public string ConstructedTitle
        {
            get { return _title; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value", "ConstructedTitle cannot be set to null.");

                _title = value;
            }
        }

        List<string> messages = new List<string>();


        public RequestObject(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            Uri = uri;
            Url = uri.ToString();
        }


        public void AddMessage(string message)
        {
            messages.Add(message);
        }

        public RequestResult CreateResult()
        {
            if (string.IsNullOrWhiteSpace(ConstructedTitle))
                return CreateResult(false);
            else
                return CreateResult(true);
        }

        public RequestResult CreateResult(bool printTitle)
        {
            if (Resource == null)
                throw new InvalidOperationException("Cannot create RequestResult if Resource is null.");

            return new RequestResult(Url,
                                     Resource.Location, Resource.Success, Resource.Exception,
                                     ConstructedTitle, printTitle,
                                     messages);
        }


        public static RequestResult Failure(Exception ex)
        {
            return new RequestResult(string.Empty, null,
                                     false, ex,
                                     string.Empty, false,
                                     new string[0]);
        }
    }

}