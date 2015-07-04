using System.Collections.Generic;
using IvionSoft;
using IvionWebSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;

[Export(typeof(IMeidoHook))]
public class IrcWeather : IMeidoHook
{
    public string Name
    {
        get { return "WeatherUnderground"; }
    }
    public string Version
    {
        get { return "0.50"; }
    }

    public Dictionary<string,string> Help
    {
        get 
        {
            return new Dictionary<string, string>()
            {
                {"w", "w [location] - Reports weather conditions at location. (Powered by WeatherUnderground)"},
                {"W", "W <location> - Sets default location for your nick."}
            };
        }
    }

    readonly IIrcComm irc;
    readonly WeatherUnderground weather;

    public void Stop()
    {}


    [ImportingConstructor]
    public IrcWeather(IIrcComm irc, IMeidoComm meido)
    {
        var conf = new Config(
            System.IO.Path.Combine(meido.ConfDir, "WebSearches.xml"), meido.CreateLogger(this));

        if (!string.IsNullOrWhiteSpace(conf.WeatherUndergroundApiKey))
            weather = new WeatherUnderground(conf.WeatherUndergroundApiKey);

        this.irc = irc;
        meido.RegisterTrigger("w", WeatherSearch);
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
                    "Precipitation {5} mm ({6} in) :: [Wind {7}] {8}",
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