using System;


namespace MeidoCommon.Formatting
{
    public static class Format
    {
        public static string Duration(TimeSpan duration)
        {
            return Duration(duration, false);
        }

        public static string DurationWithDays(TimeSpan duration)
        {
            return Duration(duration, true);
        }


        static string Duration(TimeSpan duration, bool withDays)
        {
            int days = -1;
            int hours;
            if (withDays)
            {
                days = duration.Days;
                hours = duration.Hours;
            }
            else
                hours = (int)duration.TotalHours;
            
            var minutes = duration.Minutes;
            int seconds = duration.Seconds;

            string dateStr;
            if (withDays && days > 0)
            {
                if (days > 1)
                    dateStr = string.Format("{0} days {1}:{2:00}:{3:00}", days, hours, minutes, seconds);
                else
                    dateStr = string.Format("{0} day {1}:{2:00}:{3:00}", days, hours, minutes, seconds);
            }
            else if (hours > 0)
                dateStr = string.Format("{0}:{1:00}:{2:00}", hours, minutes, seconds);
            else
                dateStr = string.Format("{0}:{1:00}", minutes, seconds);

            return dateStr;
        }


        public static string Size(long sizeInBytes)
        {
            if (sizeInBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Cannot be negative.");

            const string fmt = "#.#";

            var sizeInK = sizeInBytes / 1024d;
            if (sizeInK > 1024)
            {
                var sizeInM = sizeInK / 1024d;
                return sizeInM.ToString(fmt) + "MB";
            }

            return sizeInK.ToString(fmt) + "KB";
        }

        /// <summary>
        /// Shorten the specified lines and append continuation symbol if shortened.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if lines is null.</exception>
        /// <param name="lines">Lines.</param>
        /// <param name="maxChars">Max character count.</param>
        /// <param name="continuationSymbol">Continuation symbol.</param>
        public static string Shorten(string[] lines, int maxChars, string continuationSymbol)
        {
            return Shorten(lines, 0, maxChars, continuationSymbol);
        }

        /// <summary>
        /// Shorten the specified lines and append continuation symbol if shortened. Shorten to max lines, if longer
        /// than max char count shorten to max chars. Either one can be disabled by passing &lt;= 0.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if lines is null.</exception>
        /// <param name="lines">Lines.</param>
        /// <param name="maxLines">Max line count.</param>
        /// <param name="maxChars">Max char count.</param>
        /// <param name="continuationSymbol">Continuation symbol.</param>
        public static string Shorten(string[] lines, int maxLines, int maxChars, string continuationSymbol)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            
            bool shortenLines = maxLines > 0;
            bool shortenChars = maxChars > 0;

            string shortened;
            if (shortenLines && lines.Length > maxLines)
                shortened = string.Join(" ", lines, 0, maxLines);
            else
                shortened = string.Join(" ", lines);

            if (shortenChars && shortened.Length > maxChars)
            {
                shortened = shortened.Substring(0, maxChars);
                return string.Concat(shortened, continuationSymbol);
            }
            if (shortenLines && lines.Length > maxLines)
                return string.Concat(shortened, " ", continuationSymbol);
            
            return shortened;
        }

        /// <summary>
        /// Shorten the specified string and append continuation symbol if shortened.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if s is null.</exception>
        /// <param name="s">A string to be shortened.</param>
        /// <param name="maxChars">Max char count.</param>
        /// <param name="continuationSymbol">Continuation symbol.</param>
        public static string Shorten(string s, int maxChars, string continuationSymbol)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            if (maxChars > 0 && s.Length > maxChars)
            {
                var shortened = s.Substring(0, maxChars);
                return string.Concat(shortened, continuationSymbol);
            }

            return s;
        }
    }
}