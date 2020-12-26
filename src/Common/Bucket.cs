using System;

namespace Twitch
{
    public class Bucket
    {
        public int Size { get; init; }
        public TimeSpan RefillRate { get; init; }
    }
}
