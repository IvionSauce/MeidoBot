using System;

namespace MeidoCommon.Throttle
{
    public class RateControl
    {
        public readonly int MessageLimit;
        public readonly TimeSpan Interval;

        int counter;
        DateTime firstTime;


        public RateControl(int msgLimit, TimeSpan interval)
        {
            if (msgLimit < 2)
                throw new ArgumentOutOfRangeException(nameof(msgLimit), "Cannot be smaller than 2.");
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "Cannot be 0 or negative.");

            MessageLimit = msgLimit;
            Interval = interval;
            counter = 0;
            firstTime = DateTime.MinValue;
        }


        public static RateControl FromSeconds(int msgLimit, double intervalSecs)
        {
            if (intervalSecs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalSecs), "Cannot be 0 or negative.");

            return new RateControl(msgLimit, TimeSpan.FromSeconds(intervalSecs));
        }

        public static RateControl FromMinutes(int msgLimit, double intervalMins)
        {
            if (intervalMins <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMins), "Cannot be 0 or negative.");

            return new RateControl(msgLimit, TimeSpan.FromMinutes(intervalMins));
        }


        public bool Check(out DateTime now)
        {
            now = DateTime.MinValue;

            counter++;
            if (counter == 1)
                firstTime = DateTime.UtcNow;

            else if (counter == MessageLimit)
            {
                counter = 0;
                now = DateTime.UtcNow;
                if ( (now - firstTime) <= Interval )
                    return true;
            }

            return false;
        }

        public void Reset()
        {
            counter = 0;
        }
    }
}