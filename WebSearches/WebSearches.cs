using System.Collections.Generic;
using IvionWebSoft;
using MeidoCommon.Parsing;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class WebSearches : IMeidoHook, IPluginTriggers
{
    readonly IIrcComm irc;
    
    public string Name
    {
        get { return "WebSearches"; }
    }
    public string Version
    {
        get { return "0.34"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    public void Stop()
    {}
        
    [ImportingConstructor]
    public WebSearches(IIrcComm ircComm, IMeidoComm meido)
    {
        irc = ircComm;

        var sites = new TopicHelp[] {
            new TopicHelp(
                "searches",
                "Supported site specific searches are, with the trigger in parentheses: YouTube (yt), " +
                "Wikipedia EN (wiki), MyAnimeList (mal), AniDB (anidb), MangaUpdates (mu), Visual Novel DB (vndb), " +
                "Steam Store (steam).")
        };

        var t = TriggerThreading.Threadpool;
        Triggers = Trigger.Group(
            
            new Trigger("g", GoogleSearch, t) {
                Help = new TriggerHelp(
                    "<query>",
                    "Returns the first 3 results of a Google Search on passed query. There are also a number of " +
                    "triggers for site specific searches, see the 'searches' help subject for more information.")
                { AlsoSee = sites }
            },
            new Trigger("yt", YtSearch, t),
            new Trigger("wiki", WikiSearch, t),
            new Trigger("mal", MalSearch, t),
            new Trigger("anidb", AnidbSearch, t),
            new Trigger("mu", MuSearch, t),
            new Trigger("vndb", VndbSearch, t),
            new Trigger("steam", SteamSearch, t)
            // maybe: urbandict, dict, animenewsnetwork
        );
    }


    void GoogleSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.None);
    }

    void YtSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.YouTube);
    }

    void WikiSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.Wikipedia);
    }

    void MalSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.MyAnimeList);
    }

    void AnidbSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.AniDb);
    }

    void MuSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.MangaUpdates);
    }

    void VndbSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.VnDb);
    }

    void SteamSearch(ITriggerMsg e)
    {
        ExecuteSearch(e, Site.Steam);
    }


    void ExecuteSearch(ITriggerMsg e, Site site)
    {
        var searchTerms = e.MessageWithoutTrigger();
        if (!string.IsNullOrEmpty(searchTerms))
        {
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