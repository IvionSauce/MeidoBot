using System.IO;
using System.Net;
using System.Collections.Generic;
using IvionSoft;
using IvionWebSoft;
// Using directives for plugin use.
using MeidoCommon;
using System.ComponentModel.Composition;


[Export(typeof(IMeidoHook))]
public class IrcWeather : IMeidoHook, IPluginTriggers
{
    public string Name
    {
        get { return "WeatherService"; }
    }
    public string Version
    {
        get { return "0.58"; }
    }

    public IEnumerable<Trigger> Triggers { get; private set; }


    readonly IIrcComm irc;
    readonly ILog log;

    readonly object _locker = new object();
    OpenWeatherMap weather;

    readonly string storagePath;
    readonly Storage<string> defaultLocations;

    const string weatherError = "Weather querying disabled due to missing API key.";


    public void Stop()
    {}


    [ImportingConstructor]
    public IrcWeather(IIrcComm irc, IMeidoComm meido)
    {
        Triggers = Trigger.Group(
            
            new Trigger("w", WeatherSearch, TriggerThreading.Queue) {
                Help = new TriggerHelp(
                    "<city>,[country] | <zip>,[country] | @<nick>",
                    "Reports weather conditions at location. Location can have an optional 2-letter country code " +
                    "(ISO 3166) at the end to more precisely indicate the location. " +
                    "Will use your default location if called without arguments. (Powered by OpenWeatherMap)")
            },

            new Trigger("W", SetWeatherLocation, TriggerThreading.Queue) {
                Help = new TriggerHelp(
                    "<city>,[country] | <zip>,[country]",
                    "Sets your default weather location.")
            }
        );

        this.irc = irc;
        log = meido.CreateLogger(this);

        // Setting up configuration.
        var xmlConf = new XmlConfig2<Config>(
            Config.DefaultConfig(),
            (xml) => new Config(xml),
            log,
            Configure
        );
        meido.LoadAndWatchConfig("WeatherService.xml", xmlConf);

        // Setting up locations database/dict.
        storagePath = meido.DataPathTo("weather-locations.xml");
        try
        {
            defaultLocations = Storage<string>.Deserialize(storagePath);
        }
        catch (FileNotFoundException)
        {
            defaultLocations = new Storage<string>();
        }
    }

    void Configure(Config conf)
    {
        lock (_locker)
        {
            if (!string.IsNullOrWhiteSpace(conf.OpenWeatherMapApiKey))
            {
                weather = new OpenWeatherMap(conf.OpenWeatherMapApiKey);
            }
            else
            {
                log.Error(weatherError);
                weather = null;
            }
        }
    }


    // --- Weather Search, 'w' trigger ---

    void WeatherSearch(ITriggerMsg e)
    {
        var location = GetLocation(e);

        if (!string.IsNullOrWhiteSpace(location))
        {
            var weatherLoc = WeatherLocation.Parse(location);
            if (weatherLoc.IsValidQuery && VerifyCountry(weatherLoc.Country))
            {
                WeatherSearch(e, weatherLoc);
            }
            else
            {
                e.Reply("Invalid query format. Please use \"city, country\" or \"zip, country\", " +
                        "where country is a 2-letter country code (ISO 3166).");
            }
        }
        else
        {
            e.Reply("Either specify a location or set your default location with the 'W' trigger. " +
                    "(That is a upper case W)");
        }
    }

    void WeatherSearch(ITriggerMsg e, WeatherLocation location)
    {
        WeatherConditions cond;
        if (TryGetConditions(location, out cond))
        {
            if (cond.Success)
                irc.SendMessage(e.ReturnTo, WeatherFormat.IrcFormat(cond));
            else if (cond.HttpErrorIs(HttpStatusCode.NotFound))
                e.Reply("Sorry, couldn't find anything for query '{0}'.", location);
            else
                e.Reply(cond.Exception.Message);
        }
        else
            e.Reply(weatherError);
    }


    // --- Helper functions for WeatherSearch ---

    string GetLocation(ITriggerMsg e)
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

    static bool VerifyCountry(string country)
    {
        // No country is fine, but if we got one make sure it's
        // just 2 characters long.
        return country == null || country.Length == 2;
    }

    // Wraps locking and null-checking.
    bool TryGetConditions(WeatherLocation location, out WeatherConditions cond)
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


    // --- Set Weather Location, 'W' trigger ---

    void SetWeatherLocation(ITriggerMsg e)
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