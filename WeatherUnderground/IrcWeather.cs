using System.IO;
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
        get { return "0.56"; }
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

    public IEnumerable<Trigger> Triggers { get; private set; }


    readonly IIrcComm irc;
    readonly ILog log;

    readonly object _locker = new object();
    WeatherUnderground weather;

    readonly string storagePath;
    readonly Storage<string> defaultLocations;

    const string weatherError = "Weather querying disabled due to missing API key.";


    public void Stop()
    {}


    [ImportingConstructor]
    public IrcWeather(IIrcComm irc, IMeidoComm meido)
    {
        this.irc = irc;
        log = meido.CreateLogger(this);

        // Setting up configuration.
        var xmlConf = new XmlConfig2<Config>(
            Config.DefaultConfig(),
            (xml) => new Config(xml),
            log,
            Configure
        );
        meido.LoadAndWatchConfig("WeatherUnderground.xml", xmlConf);

        // Setting up locations database/dict.
        storagePath = meido.DataPathTo("_weatherunderground.xml");
        try
        {
            defaultLocations = Storage<string>.Deserialize(storagePath);
        }
        catch (FileNotFoundException)
        {
            defaultLocations = new Storage<string>();
        }

        Triggers = new Trigger[] {
            new Trigger("w", WeatherSearch, TriggerThreading.Queue),
            new Trigger("W", SetWeatherLocation, TriggerThreading.Queue)
        };
    }

    void Configure(Config conf)
    {
        lock (_locker)
        {
            if (!string.IsNullOrWhiteSpace(conf.WeatherUndergroundApiKey))
            {
                weather = new WeatherUnderground(conf.WeatherUndergroundApiKey);
            }
            else
            {
                log.Error(weatherError);
                weather = null;
            }
        }
    }


    // --- Weather Search, 'w' trigger ---

    void WeatherSearch(IIrcMessage e)
    {
        var location = GetLocation(e);

        if (!string.IsNullOrWhiteSpace(location))
        {
            WeatherSearch(e, location);
        }
        else
        {
            e.Reply("Either specify a location or set your default location with the 'W' trigger. " +
                    "(That is a upper case W)");
        }
    }

    void WeatherSearch(IIrcMessage e, string location)
    {
        WeatherConditions cond;
        if (TryGetConditions(location, out cond))
        {
            if (cond.Success)
                irc.SendMessage(e.ReturnTo, Format(cond));
            else
                e.Reply(cond.Exception.Message);
        }
        else
            e.Reply(weatherError);
    }


    // --- Helper functions for WeatherSearch ---

    string GetLocation(IIrcMessage e)
    {
        string location;
        if (e.MessageArray.Length > 1)
        {
            // w <location>
            location = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);

            // w @<nick>
            // Special form to query weather location associated with `nick`.
            if (location.Length > 1 && location[0] == '@')
            {
                var nick = location.Substring(1, location.Length - 1);
                location = defaultLocations.Get(nick);
            }
        }
        else
            location = defaultLocations.Get(e.Nick);

        return location;
    }

    // Wraps locking and null-checking.
    bool TryGetConditions(string location, out WeatherConditions cond)
    {
        cond = null;

        lock (_locker)
        {
            if (weather != null)
            {
                cond = weather.GetConditions(location);
                return true;
            }
        }

        return false;
    }

    static string Format(WeatherConditions cond)
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

        return string.Format("[ {0} ] {1} :: {2}°C ({3}°F) :: Humidity {4} :: " +
                             "Precipitation {5} mm ({6} in) :: [Wind {7}] {8}",
                             cond.WeatherLocation, cond.Description,
                             cond.TemperatureInC, cond.TemperatureInF, cond.RelativeHumidity,
                             cond.PrecipitationInMillimeters, cond.PrecipitationInInches,
                             cond.WindDirection, wind);
    }


    // --- Set Weather Location, 'W' trigger ---

    void SetWeatherLocation(IIrcMessage e)
    {
        if (e.MessageArray.Length > 1)
        {
            var location = string.Join(" ", e.MessageArray, 1, e.MessageArray.Length - 1);
            defaultLocations.Set(e.Nick, location);
            defaultLocations.Serialize(storagePath);

            e.Reply("Your default location has been set to '{0}'.", location);
        }
    }
}