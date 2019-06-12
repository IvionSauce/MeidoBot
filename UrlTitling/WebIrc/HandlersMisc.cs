using IvionWebSoft;


namespace WebIrc
{
    public static class MiscHandlers
    {
        public static TitlingResult YoutubeWithDuration(TitlingRequest req, string htmlDoc)
        {
            // If duration can be found, change the html info to include that.
            var ytTime = WebTools.GetYoutubeTime(htmlDoc);
            req.IrcTitle.SetHtmlTitle().AppendTime(ytTime);

            return req.CreateResult(true);
        }
    }
}