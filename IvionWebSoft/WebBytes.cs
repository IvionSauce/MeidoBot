using System;
using System.IO;
using System.Net;


namespace IvionWebSoft
{
    public class WebBytes : WebResource
    {
        public byte[] Data { get; private set; }
        public bool ContentIsHtml { get; private set; }

        public string ContentType { get; private set; }
        public long ContentLength { get; private set; }

        public string CharacterSet { get; private set; }


        public WebBytes(Uri uri, Exception ex) : base(uri, ex) {}

        public WebBytes(Uri uri, WebResponse response, int maxBytesHtml, int maxBytesOther) : base(uri)
        {
            if (response == null)
                throw new ArgumentNullException("response");

            ContentIsHtml = IsHtml(response.ContentType);

            var stream = response.GetResponseStream();
            if (ContentIsHtml)
                Data = Read(stream, maxBytesHtml);
            else
                Data = Read(stream, maxBytesOther);

            ContentType = response.ContentType;
            ContentLength = response.ContentLength;

            var httpResp = response as HttpWebResponse;
            if (httpResp != null)
            {
                CharacterSet = httpResp.CharacterSet;
            }
        }

        static byte[] Read(Stream stream, int amountToRead)
        {
            if (amountToRead > 0)
                return stream.ReadFragment(amountToRead);
            else if (amountToRead < 0)
                return stream.ReadFully();
            else
                return new byte[0];
        }


        public static WebBytes ReadOnlyHtml(WebRequest request)
        {
            return Create(request, -1, 0);
        }

        public static WebBytes Peek(WebRequest request, int peekSize)
        {
            if (peekSize < 1)
                throw new ArgumentOutOfRangeException("peekSize", "Cannot be 0 or negative.");

            return Create(request, peekSize, peekSize);
        }

        public static WebBytes ReadHtmlOrPeek(WebRequest request, int peekSize)
        {
            if (peekSize < 1)
                throw new ArgumentOutOfRangeException("peekSize", "Cannot be 0 or negative.");

            return Create(request, -1, peekSize);
        }


        public static WebBytes Create(WebRequest request)
        {
            return Create(request, -1, -1);
        }

        public static WebBytes Create(WebRequest request, int maxBytesHtml, int maxBytesOther)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            WebResponse response = null;
            try
            {
                response = request.GetResponse();

                var httpReq = request as HttpWebRequest;
                Uri location;
                if (httpReq != null)
                    location = httpReq.Address;
                else
                    location = response.ResponseUri;

                return new WebBytes(location, response, maxBytesHtml, maxBytesOther);
            }
            catch (WebException ex)
            {
                return new WebBytes(request.RequestUri, ex);
            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }
        }


        public static bool IsHtml(string contentType)
        {
            if (contentType != null)
            {
                if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
                    contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}