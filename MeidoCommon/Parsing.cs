using System;
using System.Text.RegularExpressions;


namespace MeidoCommon.Parsing
{
    public static class Parse
    {
        public static TimeSpan ShortTimeString(string shortTime)
        {
            if (shortTime == null)
                throw new ArgumentNullException(nameof(shortTime));
            
            var timeRegexp = new Regex(@"(?i)(\d*\.?\d+)([hms])");

            double seconds = 0;
            double minutes = 0;
            double hours = 0;

            foreach (Match m in timeRegexp.Matches(shortTime))
            {
                var amount = double.Parse(m.Groups[1].Value);
                var unit = m.Groups[2].Value;

                switch (unit)
                {
                    case "s":
                    seconds += amount;
                    break;

                    case "m":
                    minutes += amount;
                    break;

                    case "h":
                    hours += amount;
                    break;
                }
            }

            return TimeSpan.FromHours(hours) +
                   TimeSpan.FromMinutes(minutes) +
                   TimeSpan.FromSeconds(seconds);
        }
    }
}