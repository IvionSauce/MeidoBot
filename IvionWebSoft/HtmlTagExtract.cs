using System;
using System.Text.RegularExpressions;

namespace IvionWebSoft
{
    /// <summary>
    /// Various functions for extracting the content between certain (X)HTML/XML tags.
    /// </summary>
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
                @"(?i)(?<=<meta http-equiv=[""']?Content-Type[""']? +content=[""']text/html; ?charset=)" +
                @"[\w-]+(?=[""'] */?>)"),
            
            // HTML5 style.
            // Try to match <meta charset="UTF-8">
            new Regex(
                @"(?i)(?<=<meta charset=[""'])[\w-]+(?=[""'] */?>)"),
            
            // And because people like to make babies cry, HTML4 style - but with http-equiv and content switched around.
            // Try to match <meta content="text/html; charset=UTF-8" http-equiv="Content-Type">
            new Regex(
                @"(?i)(?<=<meta content=[""']text/html; ?charset=)" +
                @"[\w-]+(?=[""'] +http-equiv=[""']?Content-Type[""']? */?>)")
            
        };
        
        static readonly Regex[] metaRefreshRegexps =
        {
            // Try to match <meta http-equiv="Refresh" content="0;URL=http://www.e2046.com/product/18034">
            new Regex(
                @"(?i)(?<=<meta http-equiv=[""']?Refresh[""']? +content=""0; ?URL=)" +
                @"[^<>""']+(?=[""'] */?>)"),
            
            // Same as above, but with http-equiv and content switched around.
            new Regex(
                @"(?i)(?<=<meta content=""0; ?URL=)" +
                @"[^<>""']+(?=[""'] +http-equiv=[""']?Refresh[""']? */?>)")
        };
        
        
        /// <summary>
        /// Returns charset defined in the (X)HTML/XML string, if not defined or found returns null.
        /// </summary>
        /// <returns>XML charset as string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if docString is null.</exception>
        /// <param name="docString">String content of a document.</param>
        public static string GetXmlCharset(string docString)
        {
            if (docString == null)
                throw new ArgumentNullException("docString");
            
            Match charset = xmlCharsetRegexp.Match(docString);
            
            if (charset.Success)
                return charset.Value;
            else
                return null;
        }
        
        
        /// <summary>
        /// Returns the head of an HTML document, if not defined or found returns null.
        /// </summary>
        /// <returns>The content between &lthead&gt and &lt/head&gt.</returns>
        /// <exception cref="ArgumentNullException">Thrown if htmlString is null.</exception>
        /// <param name="htmlString">String content of an (X)HTML document.</param>
        public static string GetHtmlHead(string htmlString)
        {
            if (htmlString == null)
                throw new ArgumentNullException("htmlString");
            
            Match head = headRegexp.Match(htmlString);
            
            if (head.Success)
                return head.Value;
            else
                return null;
        }
        
        
        /// <summary>
        /// Returns charset defined in the (X)HTML string, if not defined or found returns null.
        /// </summary>
        /// <returns>The (X)HTML charset as string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if htmlString is null.</exception>
        /// <param name="htmlString">String content of an (X)HTML document.</param>
        public static string GetMetaCharset(string htmlString)
        {
            if (htmlString == null)
                throw new ArgumentNullException("htmlString");
            
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
        
        
        /// <summary>
        /// Returns refresh/'redirect' URL if defined in the HTML string, if not returns null.
        /// </summary>
        /// <returns>The Meta Refresh URL as string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if htmlString is null.</exception>
        /// <param name="htmlString">String content of an (X)HTML document.</param>
        public static string GetMetaRefresh(string htmlString)
        {
            if (htmlString == null)
                throw new ArgumentNullException("htmlString");
            
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
}