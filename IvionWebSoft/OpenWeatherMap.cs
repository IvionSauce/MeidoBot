using System;
using Newtonsoft.Json.Linq;

namespace IvionWebSoft
{
    public class OpenWeatherMap
    {
        public string ApiKey { get; private set; }
        readonly string owmQuery;


        public OpenWeatherMap(string apiKey)
        {
            if (apiKey == null)
                throw new ArgumentNullException(nameof(apiKey));

            ApiKey = apiKey;
            owmQuery = string.Concat(
                "https://api.openweathermap.org/data/2.5/weather?appid=", apiKey,
                "&units=metric&q="
            );
        }


        public WeatherConditions GetConditions(string location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var query = string.Concat(owmQuery, Uri.EscapeDataString(location));

            var queryResult = WebString.Download(query);
            if (!queryResult.Success)
                return new WeatherConditions(queryResult);

            var json = JObject.Parse(queryResult.Document);
            var message = (string)json["message"];

            if (string.IsNullOrEmpty(message))
            {
                return new WeatherConditions(queryResult.Location, json);
            }
            else
            {
                var ex = new JsonErrorException(message);
                return new WeatherConditions(queryResult.Location, ex);
            }
        }
    }


    public class WeatherConditions : WebResource
    {
        public string WeatherLocation { get; private set; }
        public string Description { get; private set; }

        public double TemperatureInC { get; private set; }
        public double TemperatureInF { get; private set; }
        public double RelativeHumidity { get; private set; }

        //public double PrecipitationInInches { get; private set; }
        //public double PrecipitationInMillimeters { get; private set; }

        public string WindDirection { get; private set; }

        public double WindSpeedInKph { get; private set; }
        public double WindSpeedInMph { get; private set; }


        public WeatherConditions(WebResource resource) : base(resource) {}

        public WeatherConditions(Uri uri, Exception ex) : base(uri, ex) {}

        internal WeatherConditions(Uri uri, JToken observation) : base(uri)
        {
            WeatherLocation =
                (string)observation["name"] + ", " +
                (string)observation["sys"]["country"];

            Description = (string)observation["weather"][0]["main"];

            TemperatureInC = (double)observation["main"]["temp"];
            // Convert to Fahrenheit.
            TemperatureInF = (TemperatureInC * 9/5) + 32;
            RelativeHumidity = (double)observation["main"]["humidity"];

            WindDirection = DegreesToDirection(observation["wind"]["deg"]);

            var windspeed = (double)observation["wind"]["speed"];
            // Wind speed is in m/s, convert to km/h and mph.
            WindSpeedInKph = windspeed * 3.6;
            // Approximate mph, 4 significant digits should be enough.
            WindSpeedInMph = windspeed * 2.237;
        }


        static double ToDouble(JToken el)
        {
            var str = (string)el;
            double num;
            if (double.TryParse(str, out num))
                return num;
            else
                return 0;
        }

        static string DegreesToDirection(JToken windDegrees)
        {
            int deg;
            if (int.TryParse((string)windDegrees, out deg) &&
                deg >= 0)
            {
                // Cardinal directions first, allow 10 degrees of error
                // on either side. N=0, E=90, S=180, W=270
                if (deg >= 350 || deg <= 10)
                    return "N";
                if (deg >= 80 && deg <= 100)
                    return "E";
                if (deg >= 170 && deg <= 190)
                    return "S";
                if (deg >= 260 && deg <= 280)
                    return "W";

                // Since we've done the cardinal directions above, these
                // are quite simple checks.
                if (deg < 90)
                    return "NE";
                if (deg < 180)
                    return "SE";
                if (deg < 270)
                    return "SW";
                if (deg < 360)
                    return "NW";
            }

            return string.Empty;
        }
    }
}