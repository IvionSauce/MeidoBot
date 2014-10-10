using System;
using System.IO;
using System.Net;


namespace IvionWebSoft
{
    public class MinimalWeb
    {
        public static BinaryPeek Peek(Uri uri, int peekSize)
        {
            var req = SetupRequest(uri);

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                {
                    var stream = ReadFragment(response.GetResponseStream(), peekSize);
                    return new BinaryPeek(uri, response.ContentType, response.ContentLength, stream);
                }
            }
            catch (WebException ex)
            {
                return new BinaryPeek(uri, ex);
            }
        }


        static HttpWebRequest SetupRequest(Uri uri)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.Timeout = 30000;
            req.UserAgent = "Mozilla/5.0 BinaryPeek/1.0";
            req.Accept = "*/*";
            // No AddRange, since that affects the ContentLength reported by the webserver.

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
                {
                    break;
                }
                ms.Write(buffer, 0, read);
            }
            
            ms.Position = 0;
            return ms;
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