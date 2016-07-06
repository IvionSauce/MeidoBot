using System;

namespace IvionWebSoft
{
    public class WikipediaArticle
    {
        public string Title { get; private set; }
        public string[] SummaryParagraphs { get; private set; }

        public int SectionCount
        {
            get { return sections.Length; }
        }
        readonly WikipediaSection[] sections;


        public WikipediaArticle(string title, string[] summary, WikipediaSection[] sections)
        {
            if (title == null)
                throw new ArgumentNullException("title");
            else if (summary == null)
                throw new ArgumentNullException("summary");
            else if (sections == null)
                throw new ArgumentNullException("sections");

            Title = title;
            SummaryParagraphs = summary;
            this.sections = sections;
        }


        public WikipediaSection this[int i]
        {
            get { return sections[i]; }
        }


        // Keep searching, from sectionTitle forward, for a section with paragraphs.
        // Returns null in case it couldn't find any.
        public string GetFirstParagraph(string sectionTitle)
        {
            if (sectionTitle == null)
                throw new ArgumentNullException(nameof(sectionTitle));
            
            int sectionIndex = IndexOf(sectionTitle);
            if (sectionIndex >= 0)
            {
                while (sectionIndex < SectionCount)
                {
                    var section = this[sectionIndex];
                    if (section.Paragraphs.Length > 0)
                        return section.Paragraphs[0];
                    else
                        sectionIndex++;
                }
            }
            return null;
        }

        public int IndexOf(string sectionTitle)
        {
            if (sectionTitle == null)
                throw new ArgumentNullException("sectionTitle");

            for (int i = 0; i < sections.Length; i++)
            {
                if (sectionTitle.Equals(sections[i].Title, StringComparison.OrdinalIgnoreCase) ||
                    sectionTitle.Equals(sections[i].HtmlId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }        
    }
    
    public class WikipediaSection
    {
        public string Title { get; private set; }
        public string HtmlId { get; private set; }
        public string[] Paragraphs { get; private set; }
        
        
        public WikipediaSection(string title, string htmlId, string[] paragraphs)
        {
            if (title == null)
                throw new ArgumentNullException("title");
            else if (htmlId == null)
                throw new ArgumentNullException("htmlId");
            else if (paragraphs == null)
                throw new ArgumentNullException("paragraphs");
            
            Title = title;
            HtmlId = htmlId;
            Paragraphs = paragraphs;
        }
    }
}