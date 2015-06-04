using System;
using System.Net;
using System.Text;


namespace IvionWebSoft
{
    public class WebString : WebResource
    {
        public string Document { get; private set; }
        public Encoding UsedEncoding { get; private set; }


        public WebString(WebResource resource) : base(resource) {}

        public WebString(Uri uri, Exception ex) : base(uri, ex) {}


        public WebString(Uri uri, string doc) : base(uri)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");

            Document = doc;
        }

        WebString(Uri uri, string doc, Encoding enc) : base(uri)
        {
            Document = doc;
            UsedEncoding = enc;
        }


        public WebString(Uri uri, byte[] docData, Encoding enc) : base(uri)
        {
            if (docData == null)
                throw new ArgumentNullException("docData");
            else if (enc == null)
                throw new ArgumentNullException ("enc");

            Document = enc.GetString(docData);
            UsedEncoding = enc;
        }


        public static WebString Create(WebBytes wb, Encoding fallbackEnc)
        {
            if (wb == null)
                throw new ArgumentNullException("wb");
            else if (fallbackEnc == null)
                throw new ArgumentNullException("fallbackEnc");

            if (wb.Success)
            {
                var enc = EncHelp.GetEncoding(wb.CharacterSet) ?? fallbackEnc;
                return new WebString(wb.Location, wb.Data, enc);
            }
            else
                return new WebString(wb);
        }


        public static WebString Download(string url)
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

            return Download(uri);
        }

        public static WebString Download(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            else if (!uri.IsAbsoluteUri)
                throw new ArgumentException("Uri must be absolute.");

            var wc = new WebClient();
            try
            {
                var document = wc.DownloadString(uri);
                return new WebString(uri, document, wc.Encoding);
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
}