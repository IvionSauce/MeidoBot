using System;
using IvionWebSoft;


namespace WebIrc
{
    public static class MiscHandlers
    {
        public static RequestResult WikipediaSummarize(RequestObject req, string htmlDoc)
        {
            string p = WikipediaTools.GetFirstParagraph(htmlDoc);
            if (p == string.Empty)
                return req.CreateResult(false);

            const int MaxChars = 160;
            string summary;
            if (p.Length <= MaxChars)
                summary = p.Substring(0, p.Length - 1);
            else
                summary = p.Substring(0, MaxChars) + "[...]";

            req.ConstructedTitle = string.Format("[ {0} ]", summary);
            return req.CreateResult(true);
        }


        public static RequestResult YoutubeWithDuration(RequestObject req, string htmlDoc)
        {
            // If duration can be found, change the html info to include that.
            int ytTime = WebTools.GetYoutubeTime(htmlDoc);
            if (ytTime > 0)
            {
                int ytMinutes = ytTime / 60;
                int ytSeconds = ytTime % 60;
                req.ConstructedTitle += string.Format(" [{0}:{1:00}]", ytMinutes, ytSeconds);
            }
            return req.CreateResult(true);
        }
    }
}