using System;
using IvionWebSoft;


public class Site
{
    public readonly Uri SiteUrl;
    public readonly int DisplayMax;

    public string GoogleSiteDeclaration
    {
        get { return "site:" + SiteUrl.Host; }
    }


    public static readonly Site None;
    public static readonly Site YouTube;
    public static readonly Site MyAnimeList;
    public static readonly Site AniDb;
    public static readonly Site MangaUpdates;
    public static readonly Site VnDb;

    static Site()
    {
        None = new Site();
        YouTube = new Site("https://www.youtube.com/", 3);
        MyAnimeList = new Site("http://myanimelist.net/", 2);
        AniDb = new Site("https://anidb.net/", 2);
        MangaUpdates = new Site("https://www.mangaupdates.com/", 2);
        VnDb = new Site("https://vndb.org/", 2);
    }

    Site(string url, int displayMax) : this(new Uri(url), displayMax) {}

    public Site(Uri url, int displayMax)
    {
        SiteUrl = url;
        DisplayMax = displayMax;
    }

    public Site()
    {
        SiteUrl = null;
        DisplayMax = 3;
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
}