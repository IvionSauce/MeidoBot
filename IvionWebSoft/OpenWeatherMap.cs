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

        public double PrecipitationInMillimeters { get; private set; }
        public double PrecipitationInInches { get; private set; }

        public string WindDirection { get; private set; }

        public double WindSpeedInKph { get; private set; }
        public double WindSpeedInMph { get; private set; }

        public double WindGustInKph { get; private set; }
        public double WindGustInMph { get; private set; }


        public WeatherConditions(WebResource resource) : base(resource) {}

        public WeatherConditions(Uri uri, Exception ex) : base(uri, ex) {}

        internal WeatherConditions(Uri uri, JToken observation) : base(uri)
        {
            WeatherLocation =
                (string)observation["name"] + ", " +
                (string)observation["sys"]["country"];

            Description =
                Capitalize((string)observation["weather"][0]["description"]);

            TemperatureInC = (double)observation["main"]["temp"];
            // Convert to Fahrenheit.
            TemperatureInF = (TemperatureInC * 1.8) + 32;
            RelativeHumidity = (double)observation["main"]["humidity"];

            PrecipitationInMillimeters = Precip(observation["rain"], observation["snow"]);
            // Convert to inches.
            PrecipitationInInches = PrecipitationInMillimeters / 25.4;

            WindDirection = DegreesToDirection(observation["wind"]["deg"]);

            var windspeed = (double)observation["wind"]["speed"];
            // Wind speed is in m/s, convert to km/h and mph.
            WindSpeedInKph = windspeed * 3.6;
            // Approximate mph, 4 significant digits should be enough.
            WindSpeedInMph = windspeed * 2.237;

            // Same as wind speed, but wind gust is usually absent, so
            // use `ToDouble`.
            var windgust = ToDouble(observation["wind"]["gust"], 0);
            WindGustInKph = windgust * 3.6;
            WindGustInMph = windgust * 2.237;
        }


        static string Capitalize(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                return char.ToUpper(s[0]) + s.Substring(1);
            }
            return s;
        }

        static double Precip(JToken rainEl, JToken snowEl)
        {
            // Futz rain and snow together to get total percipitation.
            // We also need to check for null because these elements are
            // usually absent.
            double sum = 0;
            if (rainEl != null)
                sum += ToDouble(rainEl["1h"], 0);
            if (snowEl != null)
                sum += ToDouble(snowEl["1h"], 0);

            return sum;
        }

        static double ToDouble(JToken el, double defaultValue)
        {
            var str = (string)el;
            double num;
            if (double.TryParse(str, out num))
                return num;
            else
                return defaultValue;
        }

        static string DegreesToDirection(JToken windDegrees)
        {
            var deg = (int)Math.Round(
                ToDouble(windDegrees, -1),
                MidpointRounding.AwayFromZero
            );
            if (deg >= 0 && deg <= 360)
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