using System;

namespace IvionWebSoft
{
    public class WebString : WebResource
    {
        public string Document { get; private set; }
        
        
        public WebString(Uri uri, string doc) :
        this(uri, true, null, doc) {}
        
        public WebString(Uri uri, Exception ex) :
        this(uri, false, ex, null) {}
        
        public WebString(Uri uri, bool success, Exception ex, string doc) : base(uri, success, ex)
        {
            Document = doc;
        }
    }


    public abstract class WebResource
    {
        public Uri Location { get; private set; }
        public bool Success { get; private set; }
        public Exception Exception { get; private set; }


        public WebResource(Uri uri, bool success, Exception ex)
        {
            Location = uri;
            Success = success;
            Exception = ex;
        }

        public WebResource(WebResource resource)
        {
            Location = resource.Location;
            Success = resource.Success;
            Exception = resource.Exception;
        }
    }
}