using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text;


namespace IvionWebSoft
{
    /// <summary>
    /// (X)HTML encoding helper. A class to help you get the content of a webpage as a string, decoded correctly.
    /// It will take both the character set reported by the HTTP headers as well as the one (if defined) in the (X)HTML
    /// document and use that information to return a correct string of the content.
    /// </summary>
    public class HtmlEncodingHelper
    {
        int _timeout = 30000;
        /// <summary>
        /// Gets or sets the timeout of the HTTP request.
        /// </summary>
        /// <value>Timeout in milliseconds.</value>
        public int Timeout
        {
            get { return _timeout; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("value", "Timeout cannot be 0 or negative.");
            }
        }

        /// <summary>
        /// The (X)HTML data used for constructing the (X)HTML content string.
        /// </summary>
        /// <value>(X)HTML data as byte array.</value>
        public byte[] HtmlData { get; private set; }

        /// <summary>
        /// The encoding that got used for the forging of the (X)HTML content string.
        /// </summary>
        public Encoding UsedEncoding { get; private set; }

        /// <summary>
        /// HTTP headers character set.
        /// </summary>
        /// <value>Verbatim string of the character set reported by the HTTP server.</value>
        public string HeadersCharset { get; private set; }
        /// <summary>
        /// The encoding as indicated by the HTTP headers. If the headers charset didn't map to an encoding it will
        /// default to ISO-8859-1.
        /// </summary>
        public Encoding EncHeaders { get; private set; }

        /// <summary>
        /// (X)HTML character set.
        /// </summary>
        /// <value>Verbatim string of the character set defined in the (X)HTML document. If not defined or found this
        /// will be null.</value>
        public string HtmlCharset { get; private set; }
        /// <summary>
        /// The encoding as indicated by the (X)HTML document. If the (X)HTML charset was not defined or couldn't be
        /// found this will be null.
        /// </summary>
        public Encoding EncHtml { get; private set; }

        Uri responseUri;
        string prelimHtmlString;

        // If key exists, return the value that properly designates the intended charset.
        static readonly Dictionary<string, string> charsetReplace =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Japanese
            {"x-jis", "shift_jis"},
            {"x-sjis", "shift_jis"},
            {"shift-jis", "shift_jis"},
            // Korean
            {"ms949", "euc-kr"},
            {"ks_c_5601-1987", "euc-kr"},
            // Hebrew
            {"iso-8859-8-i", "iso-8859-8"},
            // Western
            {"latin", "iso-8859-1"},
            // Unicode
            {"utf8", "utf-8"}
        };


        /// <summary>
        /// Download target URL into a <see cref="WebString">WebString</see>.
        /// </summary>
        /// <returns>A <see cref="WebString">WebString</see> containing the (X)HTML document.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="NotSupportedException">Thrown if scheme isn't HTTP or HTTPS.</exception>
        /// 
        /// <param name="url">URL to download.</param>
        public WebString GetWebString(string url)
        {
            return GetWebString(url, null);
        }

        /// <summary>
        /// Download target URL into a <see cref="WebString">WebString</see>. Uses provided
        /// <see cref="CookieContainer">cookies</see> in the HTTP request.
        /// </summary>
        /// <returns>A <see cref="WebString">WebString</see> containing the (X)HTML document.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="NotSupportedException">Thrown if scheme isn't HTTP or HTTPS.</exception>
        /// 
        /// <param name="url">URL to download.</param>
        /// <param name="cookies">Cookies</param>
        public WebString GetWebString(string url, CookieContainer cookies)
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

            return GetWebString(uri, cookies);
        }


        /// <summary>
        /// Download target URL into a <see cref="WebString">WebString</see>.
        /// </summary>
        /// <returns>A <see cref="WebString">WebString</see> containing the (X)HTML document.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// 
        /// <param name="url">URL to download.</param>
        public WebString GetWebString(Uri url)
        {
            return GetWebString(url, null);
        }

        /// <summary>
        /// Download target URL into a <see cref="WebString">WebString</see>. Uses provided
        /// <see cref="CookieContainer">cookies</see> in the HTTP request.
        /// </summary>
        /// <returns>A <see cref="WebString">WebString</see> containing the (X)HTML document.</returns>
        /// 
        /// <exception cref="ArgumentNullException">Thrown if url is null.</exception>
        /// <exception cref="ArgumentException">Thrown if url is relative.</exception>
        /// <exception cref="NotSupportedException">Thrown if scheme isn't HTTP or HTTPS.</exception>
        /// 
        /// <param name="url">URL to download.</param>
        /// <param name="cookies">Cookies</param>
        public WebString GetWebString(Uri url, CookieContainer cookies)
        {
            if (url == null)
                throw new ArgumentNullException("url");
            else if (!url.IsAbsoluteUri)
                throw new ArgumentException("Url must be absolute.");
            else if ( !(url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) )
                throw new NotSupportedException("Unsupported scheme, only HTTP(S) is supported.");

            try
            {
                Load(url, cookies);
            }
            catch (Exception ex)
            {
                if (ex is WebException || ex is UrlNotHtmlException)
                {
                    if (responseUri != null)
                        return new WebString(responseUri, ex);
                    else
                        return new WebString(url, ex);
                }
                else
                    throw;
            }

            string htmlContent = GetHtmlAsString();
            return new WebString(responseUri, htmlContent);
        }


        void Load(Uri url, CookieContainer cookies)
        {
            HtmlCharset = prelimHtmlString = null;
            UsedEncoding = EncHtml = null;
            responseUri = null;
            Load(url, cookies, 0);
        }

        // Load a HTML file, pointed to by an URL, into a byte array. If URL doesn't point to a HTML document
        // don't load it (we don't want to load some huge binary file).
        void Load(Uri url, CookieContainer cookies, int redirects)
        {
            // Store previous loaded HTML page for later comparison, only used in case of Meta Refresh 'redirects'.
            string oldString = prelimHtmlString;
            
            // Clear fields from (possible) previous Load call(s).
            HtmlData = null;
            HeadersCharset = prelimHtmlString = null;
            EncHeaders = null;

            HtmlData = DownloadContent(url, cookies);
            ForgePrelimString();
            
            string refreshUrl = HtmlTagExtract.GetMetaRefresh(prelimHtmlString);
            if (refreshUrl != null)
            {
                Uri redirectUrl = FixRefreshUrl(refreshUrl, url);
                
                // Only follow a HTML/"Meta Refresh" URL 10 times, we don't want to get stuck in a loop.
                const int maxRedirects = 10;
                if (redirects < maxRedirects)
                {    
                    // If during the redirects we get a different page/HTML string, refrain from following more redirects.
                    // It probably means we've arrived, but only more real world testing will tell us if that's true.
                    if (redirects > 0 && prelimHtmlString != oldString)
                        return;
                    
                    // Otherwise keep trying to follow the redirects, keeping the same cookie container.
                    Load(redirectUrl, cookies, redirects + 1);
                }
            }
        }

        // Set options and load the response stream into a byte array if it's text/html.
        byte[] DownloadContent(Uri url, CookieContainer cookies)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.Timeout = Timeout;
            req.KeepAlive = true;
            // Some sites will only respond (correctly) if they get fooled.
            req.UserAgent = "Mozilla/5.0 HTMLHelper/1.0";
            req.Accept = "*/*";
            // Some sites want to put cookies in my jar, these usually involve Meta Refresh.
            req.CookieContainer = cookies;

            using (HttpWebResponse httpResponse = (HttpWebResponse)req.GetResponse())
            {
                HeadersCharset = httpResponse.CharacterSet;
                responseUri = httpResponse.ResponseUri;
                
                // Debug
                // Console.WriteLine("Meta Redirects: {0} / {1} / {2}", redirects, httpResponse.ContentType, url);
                
                if (httpResponse.ContentType != null &&
                    httpResponse.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] data;
                    using (Stream httpStream = httpResponse.GetResponseStream())
                        data = ReadFully(httpStream);

                    return data;
                }
                else
                    throw new UrlNotHtmlException("Content-Type is not text/html.");
            }
        }


        // http://www.yoda.arachsys.com/csharp/readbinary.html
        static byte[] ReadFully(Stream stream)
        {
            const int BufferSize = 32768;
            
            byte[] buffer = new byte[BufferSize];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }


        // Forge preliminary string according to HTTP headers charset.
        void ForgePrelimString()
        {
            // Encoding according to HTTP Headers.
            // Fall back to ISO-8859-1 in case of encoding not being supported.
            try
            {
                EncHeaders = Encoding.GetEncoding( FixCharset(HeadersCharset) );
            }
            catch(ArgumentException)
            {
                EncHeaders = Encoding.GetEncoding("ISO-8859-1");
            }
            
            prelimHtmlString = EncHeaders.GetString(HtmlData);
        }


        // If charset exists in charsetReplace dict, return proper charset. Else return as-is.
        static string FixCharset(string charset)
        {
            string fixedCharset;
            if (charsetReplace.TryGetValue(charset, out fixedCharset))
                return fixedCharset;
            else
                return charset;
        }


        // Return an absolute URL from a relative Meta Refresh URL. If already absolute, return as-is.
        static Uri FixRefreshUrl(string refreshUrl, Uri referer)
        {
            if (refreshUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                refreshUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return new Uri(refreshUrl);
            else
                return new Uri(referer, refreshUrl);
        }


        string GetHtmlAsString()
        {
            // Encoding according to HTML.
            EncHtml = DetectEncodingHtml(prelimHtmlString);
            
            // If they are in agreement, return already forged string.
            // Or if the HTML doesn't have a charset declaration, return already forged string.
            if (EncHtml == EncHeaders || EncHtml == null)
            {
                UsedEncoding = EncHeaders;
                return prelimHtmlString;
            }
            // SPECIAL: If at first the headers say it's UTF-8, but the HTML declares itself to be ISO-8859-1,
            // prefer UTF-8. UTF-8 is probably the correct choice and even when it isn't, most ISO-8859-1 codepoints
            // code for the same character as the UTF-8 codepoints (as far as they overlap).
            else if (EncHeaders == Encoding.UTF8 && EncHtml == Encoding.GetEncoding("ISO-8859-1"))
            {
                UsedEncoding = EncHeaders;
                return prelimHtmlString;
            }
            // If they are not in agreement and the HTML has a charset declaration, prefer that one.
            else
            {
                UsedEncoding = EncHtml;
                return EncHtml.GetString(HtmlData);
            }
        }


        Encoding DetectEncodingHtml(string htmlString)
        {
            // First try to get the charset from Meta tag, then try the XML/XHTML approach.
            HtmlCharset = HtmlTagExtract.GetMetaCharset(htmlString);
            if (HtmlCharset == null)
            {
                HtmlCharset = HtmlTagExtract.GetXmlCharset(htmlString);
                // If both were unsuccesful, return null.
                if (HtmlCharset == null)
                    return null;
            }

            try
            {
                Encoding htmlEncoding = Encoding.GetEncoding( FixCharset(HtmlCharset) );
                return htmlEncoding;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}