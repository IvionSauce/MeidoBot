using System;


namespace MeidoBot
{
    class ThrottleInfo
    {
        public readonly int Limit;
        public readonly TimeSpan Interval;
        public readonly TimeSpan ThrottleDuration;

        public ThrottleInfo(RateControl control, TimeSpan duration)
        {
            Limit = control.Limit;
            Interval = control.Interval;
            ThrottleDuration = duration;
        }
    }


    class ThrottleControl
    {
        DateTimeOffset stopThrottle;
        public bool ThrottleActive
        {
            get
            {
                if (DateTimeOffset.Now < stopThrottle)
                    return true;
                else
                    return false;
            }
        }

        readonly RateControl[] controlRates;
        readonly TimeSpan duration;


        public ThrottleControl(RateControl[] controlRates, TimeSpan throttleDuration)
        {
            if (controlRates == null)
                throw new ArgumentNullException("controlRates");
            else if (throttleDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("throttleDuration", "Cannot be 0 or negative.");

            this.controlRates = controlRates;
            duration = throttleDuration;
        }


        public ThrottleInfo Check()
        {
            ThrottleInfo info;
            Check(out info);

            return info;
        }

        public bool Check(out ThrottleInfo info)
        {
            foreach (var control in controlRates)
            {
                DateTimeOffset now;
                if (control.Check(out now))
                {
                    stopThrottle = now + duration;
                    info = new ThrottleInfo(control, duration);

                    return true;
                }
            }

            info = null;
            return false;
        }
    }


    class RateControl
    {
        public readonly int Limit;
        public readonly TimeSpan Interval;

        int counter = 0;
        DateTimeOffset firstTime;


        public RateControl(int limit, TimeSpan interval)
        {
            if (limit < 2)
                throw new ArgumentOutOfRangeException("limit", "Cannot be smaller than 2.");
            else if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("interval", "Cannot be 0 or negative.");

            Limit = limit;
            Interval = interval;
        }


        public bool Check(out DateTimeOffset now)
        {
            now = DateTimeOffset.MinValue;

            counter++;
            if (counter == 1)
                firstTime = DateTimeOffset.Now;

            else if (counter == Limit)
            {
                counter = 0;
                now = DateTimeOffset.Now;
                if ( (now - firstTime) <= Interval )
                    return true;
            }

            return false;
        }
    }
}