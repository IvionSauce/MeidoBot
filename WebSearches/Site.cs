using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IvionWebSoft;


public class Site
{
    public readonly Uri SiteUrl;
    public readonly int DisplayMax;

    public string GoogleSiteDeclaration
    {
        get { return "site:" + SiteUrl.Host; }
    }

    public Regex SiteNameRegexp { get; set; }


    public static readonly Site None;

    public static readonly YoutubeSite YouTube;

    public static readonly Site MyAnimeList;
    public static readonly Site AniDb;
    public static readonly Site MangaUpdates;
    public static readonly Site VnDb;


    static Site()
    {
        None = new Site();
        YouTube = new YoutubeSite();

        MyAnimeList = new Site("http://myanimelist.net/", 2);
        AniDb = new Site("https://anidb.net/", 2);

        MangaUpdates = new Site("https://www.mangaupdates.com/", 2)
        { SiteNameRegexp = new Regex("^Baka-Updates Manga - ") } ;

        VnDb = new Site("https://vndb.org/", 2)
        { SiteNameRegexp = new Regex(" - The Visual Novel Database$") };
    }

    internal Site(string url, int displayMax) : this(new Uri(url), displayMax) {}

    public Site(Uri url, int displayMax)
    {
        SiteUrl = url;
        DisplayMax = displayMax;
        SiteNameRegexp = new Regex(" - [a-zA-Z.]+$");
    }

    public Site()
    {
        SiteUrl = null;
        DisplayMax = 3;
        SiteNameRegexp = null;
    }


    public SearchResults Search(string searchQuery)
    {
        string finalQuery;
        if (SiteUrl == null)
            finalQuery = searchQuery;
        else
            finalQuery = GoogleSiteDeclaration + " " + searchQuery;
        
        return GoogleTools.Search(finalQuery);
    }


    public IEnumerable<string> ProcessResults(SearchResults results)
    {
        int displayed = 0;
        foreach (var result in results)
        {
            if (displayed >= DisplayMax)
                break;

            var formatted = FormatResult(result);

            var msg = string.Format("[{0}] {1}", displayed + 1, formatted);
            yield return msg;

            displayed++;
        }
    }


    public virtual string FormatResult(SearchResult result)
    {
        var title = FormatTitle(result);

        return string.Format("{0} :: {1}", title, result.Address);
    }


    public virtual string FormatTitle(SearchResult result)
    {
        var title = GoogleTools.ReplaceBoldTags(result.Title, "\u0002", "\u000F");
        title = RemoveSiteName(title);

        return title;
    }


    public string RemoveSiteName(string pageTitle)
    {
        if (SiteNameRegexp == null)
            return pageTitle;
        else
            return SiteNameRegexp.Replace(pageTitle, string.Empty);
    }
}