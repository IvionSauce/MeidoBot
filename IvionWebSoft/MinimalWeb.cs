using System;
using System.IO;
using System.Net;


namespace IvionWebSoft
{
    public class MinimalWeb
    {
        public static BinaryPeek Peek(Uri uri, int peekSize)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            else if (!uri.IsAbsoluteUri)
                throw new ArgumentException("Uri must be absolute.");
            else if (peekSize < 1)
                throw new ArgumentOutOfRangeException("peekSize", "Cannot be 0 or negative.");

            var req = SetupRequest(uri);
            try
            {
                using (WebResponse response = req.GetResponse())
                {
                    var stream = ReadFragment(response.GetResponseStream(), peekSize);
                    return new BinaryPeek(response.ResponseUri, response.ContentType, response.ContentLength, stream);
                }
            }
            catch (WebException ex)
            {
                return new BinaryPeek(uri, ex);
            }
        }

        static WebRequest SetupRequest(Uri uri)
        {
            WebRequest req = WebRequest.Create(uri);
            req.Timeout = 30000;
            
            var wReq = req as HttpWebRequest;
            if (wReq != null)
            {
                wReq.UserAgent = "Mozilla/5.0 MinimalWeb/1.0";
                wReq.Accept = "*/*";
                // No AddRange, since that affects the ContentLength reported by the webserver.
            }
            
            return req;
        }

        // Read a fragment of the stream into a memorystream.
        static MemoryStream ReadFragment(Stream stream, int fragmentSize)
        {            
            var buffer = new byte[fragmentSize];
            var ms = new MemoryStream();
            
            while (ms.Length < fragmentSize)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;
                
                ms.Write(buffer, 0, read);
            }
            
            ms.Position = 0;
            return ms;
        }


        public static WebString SimpleGet(string url)
        {
            if (url == null)
                throw new ArgumentNullException("url");
            
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (UriFormatException ex)
            {
                return new WebString(null, ex);
            }
            
            return SimpleGet(uri);
        }

        public static WebString SimpleGet(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            else if (!uri.IsAbsoluteUri)
                throw new ArgumentException("Uri must be absolute.");

            var wc = new WebClient();
            try
            {
                var document = wc.DownloadString(uri);
                return new WebString(uri, document);
            }
            catch (WebException ex)
            {
                return new WebString(uri, ex);
            }
            finally
            {
                wc.Dispose();
            }
        }
    }


    public class BinaryPeek : WebResource, IDisposable
    {
        public string ContentType { get; private set; }
        public long ContentLength { get; private set; }
        public MemoryStream Peek { get; private set; }
        

        public BinaryPeek(Uri uri, Exception ex) : base(uri, ex) {}

        public BinaryPeek(Uri uri, string type, long length, MemoryStream peek) : base(uri)
        {
            ContentType = type;
            ContentLength = length;
            Peek = peek;
        }

        public void Dispose()
        {
            if (Peek != null)
                Peek.Dispose();
        }
    }
}