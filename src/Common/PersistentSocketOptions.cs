using System;

namespace Twitch
{
    public class PersistentSocketOptions
    {
        private readonly TimeSpan _handlerWarningTimeout = TimeSpan.FromSeconds(2);

        public Func<ISocketClient> SocketClientProvider { get; init; } = null!;

        public TimeSpan HandlerWarningTimeout
        {
            get => _handlerWarningTimeout;
            init => _handlerWarningTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(HandlerWarningTimeout), "Value must be greater than zero");
        }
    }
}
