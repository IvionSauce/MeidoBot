using IvionWebSoft;


static class WeatherFormat
{
    public static string IrcFormat(WeatherConditions cond)
    {
        // Only print percipitation if we've got some.
        string precip = string.Empty;
        if (cond.PrecipitationInMillimeters > 0)
        {
            precip = string.Format(
                "Precipitation {0:0.#} mm ({1:0.##} in) :: ",
                cond.PrecipitationInMillimeters, cond.PrecipitationInInches
            );
        }

        return string.Format(
            "[ {0} ] {1} :: {2:0.#}°C ({3:0.#}°F) :: " +
            "Humidity {4}% :: {5}{6}",
            cond.WeatherLocation, cond.Description,
            cond.TemperatureInC, cond.TemperatureInF,
            cond.RelativeHumidity, precip,
            FormatWind(cond)
        );
    }

    static string FormatWind(WeatherConditions cond)
    {
        string wind;
        if (string.IsNullOrEmpty(cond.WindDirection))
            wind = "[Wind] ";
        else
            wind = string.Format("[Wind {0}] ", cond.WindDirection);

        // Only print wind gust speeds if we got some and if they're
        // sensible, ie greater than regular wind speeds.
        string windspeeds;
        if (cond.WindGustInKph > cond.WindSpeedInKph)
        {
            windspeeds = string.Format(
                "{0:0.#}->{1:0.#} km/h ({2:0.#}->{3:0.#} mph)",
                cond.WindSpeedInKph, cond.WindGustInKph,
                cond.WindSpeedInMph, cond.WindGustInMph
            );
        }
        else
        {
            windspeeds = string.Format(
                "{0:0.#} km/h ({1:0.#} mph)",
                cond.WindSpeedInKph, cond.WindSpeedInMph
            );
        }

        return wind + windspeeds;
    }
}