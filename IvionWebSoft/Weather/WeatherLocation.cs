﻿using System;
using System.Linq;


namespace IvionWebSoft
{
    public class WeatherLocation
    {
        public bool IsCityQuery
        {
            get { return City != null; }
        }
        public bool IsZipQuery
        {
            get { return Zip > 0; }
        }
        public bool IsValidQuery
        {
            get { return IsCityQuery || IsZipQuery; }
        }

        public readonly string City;
        public readonly int Zip;
        public readonly string Country;

        const char Seperator = ',';


        public WeatherLocation()
        {
            // Leave everything default.
        }


        public WeatherLocation(string city) : this(city, -1, null)
        {
            city.ThrowIfNullOrWhiteSpace(nameof(city));
        }

        public WeatherLocation(string city, string country) : this(city, -1, country)
        {
            city.ThrowIfNullOrWhiteSpace(nameof(city));
            country.ThrowIfNullOrWhiteSpace(nameof(country));
        }


        public WeatherLocation(int zip) : this(null, zip, null)
        {
            if (zip <= 0)
                throw new ArgumentOutOfRangeException(nameof(zip), "Cannot be zero or negative.");
        }

        public WeatherLocation(int zip, string country) : this(null, zip, country)
        {
            if (zip <= 0)
                throw new ArgumentOutOfRangeException(nameof(zip), "Cannot be zero or negative.");
            country.ThrowIfNullOrWhiteSpace(nameof(country));
        }


        WeatherLocation(string city, int zip, string country)
        {
            City = city?.Trim();
            Zip = zip;
            Country = country?.Trim();
        }


        public override string ToString()
        {
            if (!IsValidQuery)
                return string.Empty;

            var place = City ?? Zip.ToString();
            if (Country == null)
                return place;
            else
                return place + Seperator + Country;
        }


        public static WeatherLocation Parse(string query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            
            var split =
                (from item in query.Split(Seperator)
                where !string.IsNullOrWhiteSpace(item)
                select item).ToArray();

            if (split.Length > 0 && split.Length < 3)
            {
                int zip;
                string city = null;
                if (!int.TryParse(split[0], out zip))
                {
                    city = split[0];
                }

                string country = null;
                if (split.Length == 2)
                    country = split[1];

                return new WeatherLocation(city, zip, country);
            }

            return new WeatherLocation();
        }
    }
}