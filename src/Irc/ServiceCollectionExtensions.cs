using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Twitch.Clients;

namespace Twitch.Irc
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTwitchIrcClient(this IServiceCollection serviceCollection, Action<TwitchIrcClientOptions>? optionsAction = null)
        {
            return serviceCollection.AddSingleton<TwitchIrcClient>(serviceProvider => CreateTwitchIrcClient(serviceProvider, optionsAction));
        }

        public static TwitchIrcClient CreateTwitchIrcClient(IServiceProvider serviceProvider, Action<TwitchIrcClientOptions>? optionsAction = null)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            var uri = new Uri("wss://irc-ws.chat.twitch.tv:443");
            var socket = new WebSocketClient(uri, loggerFactory.CreateLogger<WebSocketClient>());
            var client = new TwitchIrcClient(socket, loggerFactory.CreateLogger<TwitchIrcClient>());

            optionsAction?.Invoke(client);

            return client;
        }
    }
}
