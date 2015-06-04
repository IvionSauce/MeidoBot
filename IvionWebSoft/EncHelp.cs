using System;
using System.Text;
using System.Collections.Generic;

namespace IvionWebSoft
{
    static class EncHelp
    {
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

        public static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
        public static readonly Encoding Windows1252 = Encoding.GetEncoding(1252);


        public static Encoding GetEncoding(string charset)
        {
            if (charset == null)
                return null;

            string fixedCharset;
            if (!charsetReplace.TryGetValue(charset, out fixedCharset))
                fixedCharset = charset;

            Encoding enc;
            try
            {
                enc = Encoding.GetEncoding(fixedCharset);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // If ISO-8859-1 prefer Windows-1252, this is what most clients do as well.
            if (enc != Latin1)
                return enc;
            else
                return Windows1252;
        }

        public static string GetCharset(string htmlDoc)
        {
            if (htmlDoc == null)
                return null;

            // First try to get the charset from Meta tag, then try the XML/XHTML approach.
            string charset = HtmlTagExtract.GetMetaCharset(htmlDoc);
            if (charset == null)
                charset = HtmlTagExtract.GetXmlCharset(htmlDoc);

            return charset;
        }
    }
}