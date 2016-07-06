using System;
using IvionWebSoft;
using MeidoCommon.Formatting;


namespace WebIrc
{
    public class WikipediaHandler
    {
        public int MaxCharacters { get; set; }
        public string ContinuationSymbol { get; set; }


        public TitlingResult WikipediaSummarize(TitlingRequest req, string htmlDoc)
        {
            WikipediaArticle article = WikipediaTools.Parse(htmlDoc);

            // The paragraph to be summarized.
            string p = null;

            // Check if the URL has an anchor to a specific section in the article.
            int anchorIndex = req.Url.IndexOf("#", StringComparison.OrdinalIgnoreCase);
            if (anchorIndex >= 0 && (anchorIndex + 1) < req.Url.Length)
            {
                var anchorId = req.Url.Substring(anchorIndex + 1);
                p = article.GetFirstParagraph(anchorId);
            }
            // If no anchor or if we couldn't extract a paragraph for the specific anchor,
            // get first paragraph of the article.
            if (p == null && article.SummaryParagraphs.Length > 0)
                p = article.SummaryParagraphs[0];

            if (!string.IsNullOrWhiteSpace(p))
            {
                string summary = Format.Shorten(p, MaxCharacters, ContinuationSymbol);
                req.IrcTitle.SetFormat("[ {0} ]", summary);
            }

            return req.CreateResult(true);
        }
    }
}