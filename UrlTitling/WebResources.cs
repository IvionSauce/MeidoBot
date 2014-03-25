using System;
using System.Collections.Generic;

namespace WebHelp
{
    public class WebString : WebResource
    {
        public string Document { get; private set; }
        
        
        public WebString(Uri uri, string doc) :
        this(uri, true, null, doc) {}
        
        public WebString(Uri uri, Exception ex) :
        this(uri, false, ex, null) {}
        
        public WebString(Uri uri, bool succes, Exception ex, string doc) : base(uri, succes, ex)
        {
            Document = doc;
        }
    }


    public class WebResource
    {
        public Uri Location { get; private set; }
        public bool Succes { get; private set; }
        public Exception Exception { get; private set; }
        

        public WebResource()
        {
            Succes = false;
        }

        public WebResource(Uri uri, bool succes, Exception ex)
        {
            Location = uri;
            Succes = succes;
            Exception = ex;
        }

        public WebResource(WebResource resource)
        {
            Location = resource.Location;
            Succes = resource.Succes;
            Exception = resource.Exception;
        }
    }
}