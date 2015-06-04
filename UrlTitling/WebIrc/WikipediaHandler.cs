using System;
using IvionWebSoft;


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
                p = GetFirstParagraph(article, anchorId);
            }
            // If no anchor or if we couldn't extract a paragraph for the specific anchor,
            // get first paragraph of the article.
            if (p == null && article.SummaryParagraphs.Length > 0)
                p = article.SummaryParagraphs[0];

            if (!string.IsNullOrWhiteSpace(p))
            {
                string summary;
                if (MaxCharacters > 0 && p.Length <= MaxCharacters)
                    summary = p;
                else
                    summary = p.Substring(0, MaxCharacters) + ContinuationSymbol;

                req.ConstructedTitle.SetFormat("[ {0} ]", summary);
            }
            return req.CreateResult(true);
        }

        // Keep searching, from anchor forward, for a section with paragraphs.
        // Returns null in case it couldn't find any.
        static string GetFirstParagraph(WikipediaArticle article, string anchorId)
        {
            int sectionIndex = article.IndexOf(anchorId);
            if (sectionIndex >= 0)
            {
                while (sectionIndex < article.SectionCount)
                {
                    var section = article[sectionIndex];
                    if (section.Paragraphs.Length > 0)
                        return section.Paragraphs[0];
                    else
                        sectionIndex++;
                }
            }
            return null;
        }
    }
}