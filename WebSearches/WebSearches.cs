using System.Collections.Generic;
using IvionWebSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class WebSearches : IMeidoHook
{
    readonly IIrcComm irc;

    readonly WeatherUnderground weather;
    
    public string Prefix { get; set; }
    
    public string Name
    {
        get { return "WebSearches"; }
    }
    public string Version
    {
        get { return "0.20"; }
    }
    
    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"g", "g <search terms> - Returns the first 3 results of a Google Search on passed terms."},
                {"w", "w <location> - Reports weather conditions at location."}
            };
        }
    }


    public void Stop()
    {}
        
    [ImportingConstructor]
    public WebSearches(IIrcComm ircComm, IMeidoComm meido)
    {
        var conf = new Config(meido.ConfDir + "/WebSearches.xml");

        if (!string.IsNullOrWhiteSpace(conf.WeatherUndergroundApiKey))
            weather = new WeatherUnderground(conf.WeatherUndergroundApiKey);

        irc = ircComm;
        irc.AddTriggerHandler(Handle);
    }

    
    public void Handle(IIrcMessage e)
    {
        switch(e.Trigger)
        {
        case "g":
            GoogleSearch(e);
            return;
        case "w":
            WeatherSearch(e);
            return;
        }
    }


    void GoogleSearch(IIrcMessage e)
    {
        if (e.MessageArray.Length > 1)
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
            } // if
        } // if
    }


    void WeatherSearch(IIrcMessage e)
    {
        if (e.MessageArray.Length > 1 && weather != null)
        {
            var queryTerms = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
            var cond = weather.GetConditions(queryTerms);

            if (cond.Success)
            {
                // Only put Wind Gust in if it has a sensible value, ie higher than the normal Wind Speed.
                string wind;
                if (cond.WindGustInKph <= cond.WindSpeedInKph || cond.WindGustInMph <= cond.WindSpeedInMph)
                {
                    wind = string.Format("{0} km/h ({1} mph)", cond.WindSpeedInKph, cond.WindSpeedInMph);
                }
                else
                {
                    wind = string.Format("{0} -> {1} km/h ({2} -> {3} mph)", cond.WindSpeedInKph, cond.WindGustInKph,
                                         cond.WindSpeedInMph, cond.WindGustInMph);
                }

                var report = string.Format("[ {0} ] {1} :: {2}°C ({3}°F) :: Humidity {4} :: " +
                                           "Precipitation {5} mm ({6} in) :: Wind {7} {8}",
                                           cond.WeatherLocation, cond.Description,
                                           cond.TemperatureInC, cond.TemperatureInF, cond.RelativeHumidity,
                                           cond.PrecipitationInMillimeters, cond.PrecipitationInInches,
                                           cond.WindDirection, wind);

                irc.SendMessage(e.ReturnTo, report);
            }
            else
                e.Reply(cond.Exception.Message);
        }
    }

}