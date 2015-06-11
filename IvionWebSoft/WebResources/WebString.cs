using System;
using System.Net;
using System.Text;


namespace IvionWebSoft
{
    public class WebString : WebResource
    {
        public string Document { get; private set; }
        public Encoding UsedEncoding { get; private set; }


        WebString(WebResource resource) : base(resource) {}

        public WebString(Uri uri, Exception ex) : base(uri, ex) {}


        public WebString(Uri uri, string doc) : base(uri)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");

            Document = doc;
        }

        public WebString(Uri uri, byte[] docData, Encoding encToUse) : base(uri)
        {
            if (docData == null)
                throw new ArgumentNullException("docData");
            else if (encToUse == null)
                throw new ArgumentNullException("encToUse");

            Document = encToUse.GetString(docData);
            UsedEncoding = encToUse;
        }

        WebString(Uri uri, string doc, Encoding enc) : base(uri)
        {
            Document = doc;
            UsedEncoding = enc;
        }


        public static WebString Create(WebRequest request, Encoding fallbackEnc)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            return Create(WebBytes.Create(request), fallbackEnc);
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


        public static WebString Download(string address)
        {
            return Download(address, null);
        }

        public static WebString Download(Uri address)
        {
            return Download(address, null);
        }

        public static WebString Download(string address, Encoding encToUse)
        {
            if (address == null)
                throw new ArgumentNullException("address");

            Uri uri;
            try
            {
                uri = new Uri(address);
            }
            catch (UriFormatException ex)
            {
                return new WebString(null, ex);
            }

            return Download(uri, encToUse);
        }

        public static WebString Download(Uri address, Encoding encToUse)
        {
            if (address == null)
                throw new ArgumentNullException("address");
            else if (!address.IsAbsoluteUri)
                throw new ArgumentException("Uri must be absolute.");

            var wc = new WebClient();
            wc.Encoding = encToUse ?? Encoding.UTF8;
            try
            {
                var document = wc.DownloadString(address);
                return new WebString(address, document, wc.Encoding);
            }
            catch (WebException ex)
            {
                return new WebString(address, ex);
            }
            finally
            {
                wc.Dispose();
            }
        }
    }
}