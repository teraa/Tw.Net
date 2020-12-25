using System;

namespace Twitch.PubSub
{
    public class TwitchPubSubOptions
    {
        private readonly TimeSpan _pongTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _responseTimeout = TimeSpan.FromSeconds(5);

        public TimeSpan PongTimeout
        {
            get => _pongTimeout;
            init => _pongTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PongTimeout), "Argument must be greater than zero");
        }

        public TimeSpan PingInterval
        {
            get => _pingInterval;
            init => _pingInterval = value >= TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PingInterval), "Argument must be greater than or equal to zero");
        }

        public TimeSpan ResponseTimeout
        {
            get => _responseTimeout;
            init => _responseTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(ResponseTimeout), "Argument must be greater than zero");
        }
    }
}
