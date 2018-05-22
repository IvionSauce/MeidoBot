using System;


namespace MeidoCommon.Throttle
{
    public class ThrottleInfo
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
}