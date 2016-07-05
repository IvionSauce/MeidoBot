using System;
using IvionWebSoft;
using MeidoCommon.Formatting;


public class YoutubeSite : Site
{
    public YoutubeSite() : base("https://www.youtube.com/", 2) {}


    public override string FormatTitle(SearchResult result)
    {
        var ytPage = WebString.Download(result.Address);
        if (ytPage.Success)
        {
            var duration = WebTools.GetYoutubeTime(ytPage.Document);
            if (duration > TimeSpan.Zero)
            {
                return string.Format("{0} [{1}]",
                                     base.FormatTitle(result),
                                     Format.Duration(duration));
            }
        }

        return base.FormatTitle(result);
    }
}