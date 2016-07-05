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

            // Overflow cascade, might be too much of a hassle.
            //if (duration.Milliseconds >= 500)
            //{
            //    seconds++;

            //    if (seconds == 60)
            //    {
            //        seconds = 0;
            //        minutes++;
            //    }
            //    if (minutes == 60)
            //    {
            //        minutes = 0;
            //        hours++;
            //    }
            //    if (withDays && hours == 24)
            //    {
                    
            //    }
            //}

            if (withDays && days > 0)
                return string.Format("{0} days {1}:{2:00}:{3:00}", days, hours, minutes, seconds);
            if (hours > 0)
                return string.Format("{0}:{1:00}:{2:00}", hours, minutes, seconds);
            else
                return string.Format("{0}:{1:00}", minutes, seconds);
        }


        public static string Size(long sizeInBytes)
        {
            const string fmt = "#.#";

            var sizeInK = sizeInBytes / 1024d;
            if (sizeInK > 1024)
            {
                var sizeInM = sizeInK / 1024d;
                return sizeInM.ToString(fmt) + "MB";
            }

            return sizeInK.ToString(fmt) + "KB";
        }
    }
}