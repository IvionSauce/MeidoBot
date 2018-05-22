using System;


namespace MeidoCommon.Throttle
{
    public class ThrottleInfo
    {
        public readonly int MessageLimit;
        public readonly TimeSpan Interval;
        public readonly TimeSpan ThrottleDuration;

        public ThrottleInfo(RateControl control, TimeSpan duration)
        {
            MessageLimit = control.MessageLimit;
            Interval = control.Interval;
            ThrottleDuration = duration;
        }
    }
}