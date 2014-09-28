using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IvionWebSoft
{
    public class WeatherUnderground
    {
        string wuQuery;
        string _apikey;
        public string ApiKey
        {
            get { return _apikey; }
            set
            {
                _apikey = value;
                wuQuery = string.Concat("http://api.wunderground.com/api/", value, "/conditions/q/");
            }
        }


        public WeatherUnderground(string apiKey)
        {
            if (apiKey == null)
                throw new ArgumentNullException("apiKey");

            ApiKey = apiKey;
        }


        public WeatherConditions GetConditions(string location)
        {
            if (location == null)
                throw new ArgumentNullException("location");

            return InternalGet(location);
        }

        // Location is either US state or country.
        public WeatherConditions GetConditions(string location, string city)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            else if (city == null)
                throw new ArgumentNullException("city");

            var currentQuery = string.Concat(location, "/", city);
            return InternalGet(currentQuery);
        }


        WeatherConditions InternalGet(string query)
        {
            const string format = ".json";
            var currentQuery = string.Concat(wuQuery, query, format);

            WebString queryResult = WebTools.SimpleGetString(currentQuery);
            if (!queryResult.Success)
                return new WeatherConditions(queryResult);

            var json = JObject.Parse(queryResult.Document);
            var observation = json["current_observation"];
            var results = json["response"]["results"];
            var error = json["response"]["error"];

            // For some queries it returns a very barebones JSON string, with no observation or error.
            if (observation == null && results == null && error == null)
            {
                var ex = new JsonParseException("Server returned neither result or error.");
                return new WeatherConditions(queryResult, ex);
            }
            else if (error == null)
            {
                // Only one result.
                if (observation != null)
                {
                    return new WeatherConditions(queryResult, observation);
                }
                // Multiple results, return the first result.
                else
                {
                    var zmw = (string)results[0]["zmw"];
                    return InternalGet( "zmw:" + zmw );
                }
            }
            // Return error reported (via JSON) by the server.
            else
            {
                var errorMsg = (string)error["description"];
                var ex = new JsonErrorException(errorMsg);
                return new WeatherConditions(queryResult, ex);
            }
        }
    }


    public class WeatherConditions : WebResource
    {
        public string WeatherLocation { get; private set; }
        public string Description { get; private set; }

        public double TemperatureInC { get; private set; }
        public double TemperatureInF { get; private set; }
        public string RelativeHumidity { get; private set; }

        public double PrecipitationInInches { get; private set; }
        public double PrecipitationInMillimeters { get; private set; }

        public string WindDirection { get; private set; }

        public double WindSpeedInMph { get; private set; }
        public double WindGustInMph { get; private set; }

        public double WindSpeedInKph { get; private set; }
        public double WindGustInKph { get; private set; }


        public WeatherConditions(WebResource resource) : base(resource) {}

        public WeatherConditions(WebResource resource, Exception ex) : base(resource.Location, false, ex) {}

        public WeatherConditions(WebResource resource, JToken observation) : base(resource)
        {
            WeatherLocation = (string)observation["display_location"]["full"];
            Description = (string)observation["weather"];
            
            TemperatureInC = (double)observation["temp_c"];
            TemperatureInF = (double)observation["temp_f"];
            RelativeHumidity = (string)observation["relative_humidity"];

            PrecipitationInInches = ToDouble( observation["precip_today_in"] );
            PrecipitationInMillimeters = ToDouble( observation["precip_today_metric"] );
            
            WindDirection = (string)observation["wind_dir"];
            
            WindSpeedInMph = (double)observation["wind_mph"];
            WindGustInMph = (double)observation["wind_gust_mph"];
            
            WindSpeedInKph = (double)observation["wind_kph"];
            WindGustInKph = (double)observation["wind_gust_kph"];
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
    }
}