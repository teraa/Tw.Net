using System;
using System.Text;

namespace Twitch.Irc
{
    public class TwitchIrcOptions : PersistentSocketOptions
    {
        private readonly TimeSpan _pongTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _loginTimeout = TimeSpan.FromSeconds(5);

        public TwitchIrcOptions()
        {
            SocketClientProvider = () => new TextWebSocketClient(new Uri("wss://irc-ws.chat.twitch.tv:443"), new UTF8Encoding(false));
        }

        public TimeSpan PongTimeout
        {
            get => _pongTimeout;
            init => _pongTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PongTimeout), "Value must be greater than zero");
        }

        public TimeSpan PingInterval
        {
            get => _pingInterval;
            init => _pingInterval = value >= TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PingInterval), "Value must be greater than or equal to zero");
        }

        public TimeSpan LoginTimeout
        {
            get => _loginTimeout;
            init => _loginTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(LoginTimeout), "Value must be greater than zero");
        }

        public bool RequestMembershipCapability { get; init; } = false;

        public Func<LimitInfo, IRateLimiter> RateLimiterProvider { get; init; } = i => new SlidingWindowRateLimiter(i.Limit, i.Interval);

        public LimitInfo JoinLimit { get; init; } = new LimitInfo(20, TimeSpan.FromSeconds(10));

        public LimitInfo CommandLimit { get; init; } = new LimitInfo(20, TimeSpan.FromSeconds(30));
    }
}
