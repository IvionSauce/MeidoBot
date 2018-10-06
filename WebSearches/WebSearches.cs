using System.Collections.Generic;
using IvionWebSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class WebSearches : IMeidoHook
{
    readonly IIrcComm irc;
    
    public string Name
    {
        get { return "WebSearches"; }
    }
    public string Version
    {
        get { return "0.30"; }
    }
    
    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"g", "g <search terms> - Returns the first 3 results of a Google Search on passed terms."}
            };
        }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    public void Stop()
    {}
        
    [ImportingConstructor]
    public WebSearches(IIrcComm ircComm, IMeidoComm meido)
    {
        irc = ircComm;

        var t = TriggerThreading.Threadpool;

        Triggers = new Trigger[] {
            new Trigger("g", GoogleSearch, t),
            new Trigger("yt", YtSearch, t),
            new Trigger("wiki", WikiSearch, t),
            new Trigger("mal", MalSearch, t),
            new Trigger("anidb", AnidbSearch, t),
            new Trigger("mu", MuSearch, t),
            new Trigger("vndb", VndbSearch, t),
            new Trigger("steam", SteamSearch, t)
            // maybe: urbandict, dict, animenewsnetwork
        };
    }


    void GoogleSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.None);
    }

    void YtSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.YouTube);
    }

    void WikiSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.Wikipedia);
    }

    void MalSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.MyAnimeList);
    }

    void AnidbSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.AniDb);
    }

    void MuSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.MangaUpdates);
    }

    void VndbSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.VnDb);
    }

    void SteamSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.Steam);
    }


    void ExecuteSearch(IIrcMessage e, Site site)
    {
        if (e.MessageArray.Length > 1)
        {
            var searchTerms = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
            var results = site.Search(searchTerms);

            if (results.Success)
            {
                if (results.Count > 0)
                    PrintResults(results, site, e.ReturnTo);
                else
                    e.Reply("Sorry, the query '{0}' didn't result in any hits.", searchTerms);
            }
            else
                e.Reply("Error executing search: " + results.Exception.Message);
        }
    }

    void PrintResults(SearchResults results, Site site, string target)
    {
        foreach (var msg in site.ProcessResults(results))
            irc.SendMessage(target, msg);
    }
}