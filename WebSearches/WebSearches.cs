using System.Collections.Generic;
using IvionWebSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class WebSearches : IMeidoHook
{
    readonly IIrcComm irc;
    
    public string Prefix { get; set; }
    
    public string Name
    {
        get { return "WebSearches"; }
    }
    public string Version
    {
        get { return "0.10"; }
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
    public WebSearches(IIrcComm ircComm)
    {
        irc = ircComm;
        irc.AddTriggerHandler(Handle);
    }

    
    public void Handle(IIrcMessage e)
    {
        if (e.Trigger == "g" && e.MessageArray.Length > 1)
        {
            var searchTerms = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
            var results = GoogleTools.Search(searchTerms);

            if (results.Success)
            {
                const int maxDisplayed = 3;
                int displayed = 0;
                foreach (var result in results)
                {
                    if (displayed >= maxDisplayed)
                        break;

                    var title = GoogleTools.ReplaceBoldTags(result.Title, "\u0002", "\u000F");
                    var msg = string.Format("[{0}] {1} :: {2}", displayed + 1, title, result.Address);
                    irc.SendMessage(e.ReturnTo, msg);

                    displayed++;
                }
            }
        }
    }

}