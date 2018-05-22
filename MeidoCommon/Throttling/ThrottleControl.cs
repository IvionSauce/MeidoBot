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
                if (stopThrottle > DateTime.MinValue && DateTime.UtcNow < stopThrottle)
                    return true;
                
                stopThrottle = DateTime.MinValue;
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


        public ThrottleControl(RateControl[] controlRates, TimeSpan throttleDuration)
        {
            if (controlRates == null)
                throw new ArgumentNullException(nameof(controlRates));
            if (throttleDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(throttleDuration), "Cannot be 0 or negative.");

            this.controlRates = controlRates;
            duration = throttleDuration;
        }


        public static ThrottleControl FromMinutes(RateControl[] controlRates, double throttleMins)
        {
            if (throttleMins <= 0)
                throw new ArgumentOutOfRangeException(nameof(throttleMins), "Cannot be 0 or negative.");

            return new ThrottleControl(controlRates, TimeSpan.FromMinutes(throttleMins));
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