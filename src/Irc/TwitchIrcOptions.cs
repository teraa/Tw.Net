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

        public Bucket JoinBucket { get; init; } = new Bucket { Size = 20, RefillRate = TimeSpan.FromSeconds(10) };

        public Bucket CommandBucket { get; init; } = new Bucket { Size = 20, RefillRate = TimeSpan.FromSeconds(30) };
    }
}
