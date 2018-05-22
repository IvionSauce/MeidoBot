using System;


namespace MeidoCommon.Throttle
{
    public class RateControl
    {
        public readonly int Limit;
        public readonly TimeSpan Interval;

        int counter = 0;
        DateTime firstTime;


        public RateControl(int limit, double intervalSecs) : this(limit, TimeSpan.FromSeconds(intervalSecs)) {}

        public RateControl(int limit, TimeSpan interval)
        {
            if (limit < 2)
                throw new ArgumentOutOfRangeException(nameof(limit), "Cannot be smaller than 2.");
            else if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "Cannot be 0 or negative.");

            Limit = limit;
            Interval = interval;
        }


        public bool Check(out DateTime now)
        {
            now = DateTime.MinValue;

            counter++;
            if (counter == 1)
                firstTime = DateTime.UtcNow;

            else if (counter == Limit)
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