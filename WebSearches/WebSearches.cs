using System.Collections.Generic;
using System.IO;
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
        get { return "0.22"; }
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


    public void Stop()
    {}
        
    [ImportingConstructor]
    public WebSearches(IIrcComm ircComm, IMeidoComm meido)
    {
        irc = ircComm;
        meido.RegisterTrigger("g", GoogleSearch);
        meido.RegisterTrigger("yt", YtSearch);
        meido.RegisterTrigger("mal", MalSearch);
        meido.RegisterTrigger("anidb", AnidbSearch);
        meido.RegisterTrigger("mu", MuSearch);
        // yes: mal, anidb, mangaupdates(mu), youtube(yt)
        // maybe: wikipedia, urbandict, dict, animenewsnetwork
    }


    void GoogleSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.None);
    }

    void YtSearch(IIrcMessage e)
    {
        ExecuteSearch(e, Site.YouTube);
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


    void ExecuteSearch(IIrcMessage e, Site site)
    {
        if (e.MessageArray.Length > 1)
        {
            var searchTerms = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
            var results = site.Search(searchTerms);

            if (results.Success)
                ProcessSearch(results, site.DisplayMax, e.ReturnTo);
            else
                e.Reply("Error executing search: " + results.Exception.Message);
        }
    }


    void ProcessSearch(SearchResults results, int displayMax, string target)
    {
        int displayed = 0;
        foreach (var result in results)
        {
            if (displayed >= displayMax)
                break;

            var title = GoogleTools.ReplaceBoldTags(result.Title, "\u0002", "\u000F");
            var msg = string.Format("[{0}] {1} :: {2}", displayed + 1, title, result.Address);
            irc.SendMessage(target, msg);

            displayed++;
        }
    }
}