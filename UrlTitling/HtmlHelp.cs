using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;


namespace HtmlHelp
{
    public class HtmlEncodingHelper
    {
        string headersCharset;
        string prelimHtmlString;

        // Time out with a default value of 30 seconds.
        int _timeout = 30000;
        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        public byte[] HtmlData { get; private set; }
        public Encoding UsedEncoding { get; private set; }
        public Encoding EncHeaders { get; private set; }
        public Encoding EncHtml { get; private set; }

        // If key exists, return the value that properly designates the intended charset.
        static readonly Dictionary<string, string> charsetReplace = new Dictionary<string, string> ()
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
            {"latin", "iso-8859-1"}
        };


        // If charset exists in charsetReplace dict, return proper charset. Else return the string in lowercase.
        static string FixCharset(string charset)
        {
            string charsetLow = charset.ToLower();
            string fixedCharset;
            if (charsetReplace.TryGetValue(charsetLow, out fixedCharset))
                return fixedCharset;
            else
                return charsetLow;
        }

        // Return an absolute URL from a relative Meta Refresh URL. If already absolute, return as-is.
        static Uri FixRefreshUrl(string refreshUrl, Uri refUrl)
        {
            if (refreshUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return new Uri(refreshUrl);
            else
                return new Uri(refUrl, refreshUrl);
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
            if (HtmlData != null)
            {
                // Encoding according to HTTP Headers.
                // Fall back to ISO-8859-1 in case of encoding not being supported.
                string fixedCharset = FixCharset(headersCharset);
                try
                {
                    EncHeaders = Encoding.GetEncoding(fixedCharset);
                }
                catch(ArgumentException)
                {
                    EncHeaders = Encoding.GetEncoding("ISO-8859-1");
                }

                prelimHtmlString = EncHeaders.GetString(HtmlData);
            }
        }

        public void Load(Uri url)
        {
            Load(url, null);
        }

        public void Load(Uri url, CookieContainer cookies)
        {
            prelimHtmlString = null;
            UsedEncoding = EncHtml = null;
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
            headersCharset = prelimHtmlString = null;
            EncHeaders = null;

            // Create the HTTP request and set some options.
            HttpWebRequest req = CreateRequest(url, cookies);

            // Get HTTP response and stream and read the stream into a byte array if it's a HTML file.
            using (HttpWebResponse httpResponse = (HttpWebResponse)req.GetResponse())
            {
                headersCharset = httpResponse.CharacterSet;

                // Debug
                // Console.WriteLine("Meta Redirects: {0} / {1} / {2}", redirects, httpResponse.ContentType, url);

                if (httpResponse.ContentType.StartsWith("text/html"))
                {
                    using (Stream httpStream = httpResponse.GetResponseStream())
                        HtmlData = ReadFully(httpStream);
                }
                // If not text/html, return without doing anything (leaving HtmlData null).
                else
                    return;
            }

            // Forge preliminary HTML string based on the charset reported by the HTTP headers.
            ForgePrelimString();

            string refreshUrl = HtmlTagExtract.GetMetaRefresh(prelimHtmlString);
            if (refreshUrl != null)
            {
                Uri redirectUrl = FixRefreshUrl(refreshUrl, url);

                // Only follow a HTML/"Meta Refresh" URL 10 times, we don't want to get stuck in a loop.
                if (redirects < 10)
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

        HttpWebRequest CreateRequest(Uri url, CookieContainer cookies)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.Timeout = Timeout;
            req.KeepAlive = true;
            // Some sites will only respond (correctly) if they get fooled.
            req.UserAgent = "Mozilla/5.0 HTMLHelper/0.90";
            // Some sites want to put cookies in my jar, these usually involve Meta Refresh.
            req.CookieContainer = cookies;

            return req;
        }

        public static Encoding DetectEncodingHtml(string htmlString)
        {
            Encoding htmlEncoding = null;

            // First try to get the charset from Meta tag, then try the XML/XHTML approach.
            string htmlCharset = HtmlTagExtract.GetMetaCharset(htmlString);
            if (htmlCharset == null)
            {
                htmlCharset = HtmlTagExtract.GetXmlCharset(htmlString);
                // If both were unsuccesful, return null.
                if (htmlCharset == null)
                    return null;
            }

            string fixedCharset = FixCharset(htmlCharset);

            // Debug
            // Console.WriteLine("HTML charset string: " + fixedCharset);

            try
            {
                htmlEncoding = Encoding.GetEncoding(fixedCharset);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return htmlEncoding;
        }

        // What it says on the tin. ;D
        public string GetHtmlAsString()
        {
            if (HtmlData == null)
                return null;
            else
            {
                // Encoding according to HTML.
                EncHtml = DetectEncodingHtml(prelimHtmlString);

                // Debug
                Console.WriteLine("(HTTP) {0} -> {1} ;(HTML) {2}", headersCharset, EncHeaders, EncHtml);

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
        }
    }


    public static class HtmlTagExtract
    {
        // XML and alternative XHTML style.
        // Try to match <?xml version="1.0" encoding="UTF-8"?>
        static readonly Regex xmlCharsetRegexp =
            new Regex(
                @"(?i)(?<=<\? ?xml version=[""']1.0[""'] encoding=[""'])" + 
                @"[\w-]+(?=[""'] ?\?>)");

        static readonly Regex headRegexp =
            new Regex(
                @"(?i)(?s)(?<=<head[^<>]*>).*?(?=</head>\s*<)");

        static readonly Regex[] charsetRegexps =
        {
            // HTML4 style.
            // Try to match <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
            new Regex(
                @"(?i)(?<=<meta http-equiv=[""']Content-Type[""'] +content=[""']text/html; ?charset=)" +
                @"[\w-]+(?=[""'] */?>)"),

            // HTML5 style.
            // Try to match <meta charset="UTF-8">
            new Regex(
                @"(?i)(?<=<meta charset=[""'])[\w-]+(?=[""'] */?>)"),

            // And because people like to make babies cry, HTML4 style - but with http-equiv and content switched around.
            // Try to match <meta content="text/html; charset=UTF-8" http-equiv="Content-Type">
            new Regex(
                @"(?i)(?<=<meta content=[""']text/html; ?charset=)" +
                @"[\w-]+(?=[""'] +http-equiv=[""']Content-Type[""'] */?>)")

        };

        static readonly Regex[] metaRefreshRegexps =
        {
            // Try to match <meta http-equiv="Refresh" content="0;URL=http://www.e2046.com/product/18034">
            new Regex(
                @"(?i)(?<=<meta http-equiv=[""']Refresh[""'] +content=""0; ?URL=)" +
                @"[^<>""']+(?=[""'] */?>)"),

            // Same as above, but with http-equiv and content switched around.
            new Regex(
                @"(?i)(?<=<meta content=""0; ?URL=)" +
                @"[^<>""']+(?=[""'] +http-equiv=[""']Refresh[""'] */?>)")
        };


        // Returns charset defined in the (X)HTML/XML string, if not defined or found returns null.
        public static string GetXmlCharset(string docString)
        {
            Match charset = xmlCharsetRegexp.Match(docString);

            if (charset.Success)
                return charset.Value;
            else
                return null;
        }

        // Try to isolate the head of the HTML document.
        public static string GetHtmlHead(string htmlString)
        {
            Match head = headRegexp.Match(htmlString);

            if (head.Success)
                return head.Value;
            else
                return null;
        }

        // Returns charset defined in the (X)HTML string, if not defined or found returns null.
        public static string GetMetaCharset(string htmlString)
        {
            string head = GetHtmlHead(htmlString);
            if (head == null)
                return null;

            foreach (Regex regexp in charsetRegexps)
            {
                Match charset = regexp.Match(head);

                if (charset.Success)
                    return charset.Value;
            }
            return null;
        }

        // Returns refresh/'redirect' URL if defined in the HTML string, if not returns null.
        public static string GetMetaRefresh(string htmlString)
        {
            string head = GetHtmlHead(htmlString);
            if (head == null)
                return null;

            foreach (Regex regexp in metaRefreshRegexps)
            {
                Match metaRefresh = regexp.Match(head);

                if (metaRefresh.Success)
                    return metaRefresh.Value;
            }
            return null;
        }
    }

/* using System.Net.Security;
using System.Security.Cryptography.X509Certificates; */

    class Test
    {
        /* static bool TrustAllCertificates(object sender, X509Certificate cert, X509Chain chain,
                                         SslPolicyErrors sslPolicyErrors)
        {
            return true;
        } */

        static void Main(string[] args)
        {
            // ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificates;

            var bla = new HtmlEncodingHelper();
            bla.Load( new Uri(args[0]) );

            string htmlString = bla.GetHtmlAsString();
            if (htmlString != null)
                Console.WriteLine(htmlString);
        }
    }
}