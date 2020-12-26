using System;
using System.Text;

namespace Twitch
{
    public class PersistentSocketOptions
    {
        private readonly TimeSpan _handlerWarningTimeout = TimeSpan.FromSeconds(2);

        public Func<ISocketClient> SocketClientProvider { get; init; }
            = () => new TextWebSocketClient(new Uri("wss://irc-ws.chat.twitch.tv:443"), new UTF8Encoding(false));

        public TimeSpan HandlerWarningTimeout
        {
            get => _handlerWarningTimeout;
            init => _handlerWarningTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(HandlerWarningTimeout), "Value must be greater than zero");
        }
    }
}
