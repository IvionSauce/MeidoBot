using System;


namespace IvionWebSoft
{
    public abstract class WebResource
    {
        public Uri Location { get; private set; }
        public bool Success { get; private set; }
        public Exception Exception { get; private set; }


        public WebResource(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            Location = uri;
            Success = true;
            Exception = null;
        }

        public WebResource(Exception ex) : this(null, ex) {}

        public WebResource(Uri uri, Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException("ex");

            Location = uri;
            Success = false;
            Exception = ex;
        }

        public WebResource(WebResource resource)
        {
            if (resource == null)
                throw new ArgumentNullException("resource");

            Location = resource.Location;
            Success = resource.Success;
            Exception = resource.Exception;
        }
    }
}