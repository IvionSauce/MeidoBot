using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IvionWebSoft;


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


    public class TitlingRequest
    {
        public readonly Uri Uri;
        public readonly string Url;

        public WebResource Resource { get; set; }

        TitleConstruct _title = new TitleConstruct();
        public TitleConstruct ConstructedTitle
        {
            get { return _title; }
        }

        List<string> messages = new List<string>();



        public TitlingRequest(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            else if (!uri.IsAbsoluteUri)
                throw new ArgumentException("Uri must be absolute: " + uri);
            else if (!IsSchemeSupported(uri))
                throw new NotSupportedException("Unsupported scheme, only HTTP(s) and FTP are supported: " + uri);

            Uri = uri;
            Url = uri.OriginalString;
        }


        public static bool IsSchemeSupported(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            switch (uri.Scheme)
            {
            case "http":
            case "https":
            case "ftp":
                return true;
            default:
                return false;
            }
        }


        public void AddMessage(string message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            messages.Add(message);
        }


        public TitlingResult CreateResult()
        {
            return CreateResult(true);
        }

        public TitlingResult CreateResult(bool printTitle)
        {
            if (Resource == null)
                throw new InvalidOperationException("Cannot create TitlingResult if Resource is null.");

            return new TitlingResult(Url,
                                     Resource.Location, Resource.Success, Resource.Exception,
                                     ConstructedTitle.ToString(), printTitle,
                                     messages);
        }
    }

}