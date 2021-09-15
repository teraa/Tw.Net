using Microsoft.Extensions.Logging;
using System;
using Twitch.Clients;

namespace Twitch.Irc
{
    public static class SocketClientFactory
    {
        public static ISocketClient CreateDefault(ILoggerFactory loggerFactory)
            => new WebSocketClient(new Uri("wss://irc-ws.chat.twitch.tv:443"), loggerFactory.CreateLogger<WebSocketClient>());
    }
}
