using System;

namespace Twitch
{
    public class LimitInfo
    {
        public int Limit { get; }
        public TimeSpan Interval { get; }

        public LimitInfo(int count, TimeSpan interval)
        {
            Limit = count;
            Interval = interval;
        }
    }
}
