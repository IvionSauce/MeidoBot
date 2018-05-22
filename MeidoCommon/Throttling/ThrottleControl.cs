using System;


namespace MeidoCommon.Throttle
{
    public class ThrottleControl
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
                throw new ArgumentNullException(nameof(controlRates));
            else if (throttleDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(throttleDuration), "Cannot be 0 or negative.");

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
}