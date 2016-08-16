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
        DateTime stopThrottle;
        public bool ThrottleActive
        {
            get
            {
                if (DateTime.UtcNow < stopThrottle)
                    return true;
                else
                    return false;
            }
        }

        public TimeSpan TimeLeft
        {
            get
            {
                var timeleft = stopThrottle - DateTime.UtcNow;
                if (timeleft > TimeSpan.Zero)
                    return timeleft;
                else
                    return TimeSpan.Zero;
            }
        }

        readonly RateControl[] controlRates;
        readonly TimeSpan duration;


        public ThrottleControl(RateControl[] controlRates, double throttleMinutes) :
        this(controlRates, TimeSpan.FromMinutes(throttleMinutes)) {}

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
                DateTime now;
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


        public void Reset()
        {
            foreach (var control in controlRates)
                control.Reset();

            stopThrottle = DateTime.MinValue;
        }
    }


    class RateControl
    {
        public readonly int Limit;
        public readonly TimeSpan Interval;

        int counter = 0;
        DateTime firstTime;


        public RateControl(int limit, double intervalSecs) : this(limit, TimeSpan.FromSeconds(intervalSecs)) {}

        public RateControl(int limit, TimeSpan interval)
        {
            if (limit < 2)
                throw new ArgumentOutOfRangeException("limit", "Cannot be smaller than 2.");
            else if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("interval", "Cannot be 0 or negative.");

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