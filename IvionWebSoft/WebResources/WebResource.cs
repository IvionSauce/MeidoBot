using System;
using System.Net;


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


        public bool TryGetHttpError(out HttpStatusCode error)
        {
            var webEx = Exception as WebException;
            if (webEx != null && webEx.Status == WebExceptionStatus.ProtocolError)
            {
                var resp = webEx.Response as HttpWebResponse;
                if (resp != null)
                {
                    error = resp.StatusCode;
                    return true;
                }
            }

            error = default(HttpStatusCode);
            return false;
        }

        public bool HttpErrorIs(HttpStatusCode error)
        {
            HttpStatusCode status;
            if (TryGetHttpError(out status))
                return status == error;

            return false;
        }
    }
}