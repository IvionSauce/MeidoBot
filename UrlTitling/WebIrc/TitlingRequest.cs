using System;
using System.Collections.Generic;
using IvionWebSoft;


namespace WebIrc
{
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