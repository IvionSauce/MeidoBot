using System;
using System.Text;

namespace IvionWebSoft
{
    public class HtmlPage : WebResource
    {
        /// <summary>
        /// The contents of the HTML page.
        /// </summary>
        public string Content { get; private set; }

        /// <summary>
        /// The encoding that got used for the forging of the (X)HTML content string.
        /// </summary>
        public Encoding UsedEncoding { get; private set; }

        /// <summary>
        /// Verbatim string of the character set reported by the HTTP server. Can be null if not supplied.
        /// </summary>
        public string HeadersCharset { get; private set; }
        /// <summary>
        /// The encoding as indicated by the HTTP headers. If the headers charset didn't map to an encoding it will
        /// default to Windows-1252.
        /// </summary>
        public Encoding EncHeaders { get; private set; }

        /// <summary>
        /// Verbatim string of the character set defined in the (X)HTML document. If not defined or found this
        /// will be null.
        /// </summary>
        public string HtmlCharset { get; private set; }
        /// <summary>
        /// The encoding as indicated by the (X)HTML document. If the (X)HTML charset was not defined or couldn't be
        /// found this will be null.
        /// </summary>
        public Encoding EncHtml { get; private set; }


        public HtmlPage(WebResource resource) : base(resource) {}

        public HtmlPage(Uri uri, Exception ex) : base(uri, ex) {}


        public HtmlPage(Uri uri, byte[] htmlData, string httpHeadersCharset) : base(uri)
        {
            if (htmlData == null)
                throw new ArgumentNullException("htmlData");

            HeadersCharset = httpHeadersCharset;
            EncHeaders = EncHelp.GetEncoding(HeadersCharset) ?? EncHelp.Windows1252;

            var prelimContent = EncHeaders.GetString(htmlData);

            DetermineEncoding(htmlData, prelimContent);
        }


        internal HtmlPage(WebBytes wb, WebString prelimContent) : base(wb.Location)
        {
            HeadersCharset = wb.CharacterSet;
            EncHeaders = prelimContent.UsedEncoding;

            DetermineEncoding(wb.Data, prelimContent.Document);
        }


        void DetermineEncoding(byte[] contentData, string prelimContent)
        {
            HtmlCharset = EncHelp.GetCharset(prelimContent);
            EncHtml = EncHelp.GetEncoding(HtmlCharset);

            // If they are in agreement or if the HTML doesn't have a charset declaration, use already forged string.
            if (EncHtml == EncHeaders || EncHtml == null)
            {
                UsedEncoding = EncHeaders;
                Content = prelimContent;
            }
            // If they are not in agreement and the HTML has a charset declaration, reforge string.
            else
            {
                UsedEncoding = EncHtml;
                Content = EncHtml.GetString(contentData);
            }
        }


        public static HtmlPage Create(System.Net.WebRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            return Create( WebBytes.ReadOnlyHtml(request) );
        }

        public static HtmlPage Create(WebBytes wb)
        {
            if (wb == null)
                throw new ArgumentNullException("wb");

            if (wb.ContentIsHtml)
                return new HtmlPage(wb.Location, wb.Data, wb.CharacterSet);
            else if (wb.Success)
            {
                var ex = new NotHtmlException("Content isn't (X)HTML. Content-Type: " + wb.ContentType);
                return new HtmlPage(wb.Location, ex);
            }
            else
                return new HtmlPage(wb);
        }
    }
}