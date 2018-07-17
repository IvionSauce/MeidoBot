﻿using System.IO;
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
        get { return "0.52"; }
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

    readonly string storagePath;
    readonly Storage<string> defaultLocations;


    public void Stop()
    {}


    [ImportingConstructor]
    public IrcWeather(IIrcComm irc, IMeidoComm meido)
    {
        storagePath = meido.DataPathTo("_weatherunderground.xml");
        try
        {
            defaultLocations = Storage<string>.Deserialize(storagePath);
        }
        catch (FileNotFoundException)
        {
            defaultLocations = new Storage<string>();
        }


        var log = meido.CreateLogger(this);
        var conf = new Config(meido.ConfPathTo("WeatherUnderground.xml"), log);

        if (!string.IsNullOrWhiteSpace(conf.WeatherUndergroundApiKey))
        {
            weather = new WeatherUnderground(conf.WeatherUndergroundApiKey);
            meido.RegisterTrigger("w", WeatherSearch);
        }
        else
            log.Message("Weather querying disabled due to missing API key.");

        this.irc = irc;
        meido.RegisterTrigger("W", SetWeatherLocation);
    }


    void WeatherSearch(IIrcMessage e)
    {
        var location = GetLocation(e);

        if (!string.IsNullOrWhiteSpace(location))
        {
            var cond = weather.GetConditions(location);

            if (cond.Success)
                irc.SendMessage(e.ReturnTo, Format(cond));
            else
                e.Reply(cond.Exception.Message);
        }
        else
            e.Reply("Either specify a location or set your default location with the 'W' trigger. " +
                    "(That is a upper case W)");
    }

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

    string Format(WeatherConditions cond)
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