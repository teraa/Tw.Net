using System;

namespace Twitch
{
    public class LimitInfo
    {
        public int Count { get; }
        public TimeSpan Interval { get; }

        public LimitInfo(int count, TimeSpan interval)
        {
            Count = count;
            Interval = interval;
        }
    }
}
