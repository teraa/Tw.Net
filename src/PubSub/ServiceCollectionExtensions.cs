using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Twitch.Clients;

namespace Twitch.PubSub
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTwitchPubSubClient(this IServiceCollection serviceCollection, Action<TwitchPubSubClientOptions>? optionsAction = null)
        {
            return serviceCollection.AddSingleton<TwitchPubSubClient>(serviceProvider => CreateTwitchPubSubClient(serviceProvider, optionsAction));
        }

        public static TwitchPubSubClient CreateTwitchPubSubClient(IServiceProvider serviceProvider, Action<TwitchPubSubClientOptions>? optionsAction = null)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            var uri = new Uri("wss://pubsub-edge.twitch.tv");
            var socket = new WebSocketClient(uri, loggerFactory.CreateLogger<WebSocketClient>());
            var client = new TwitchPubSubClient(socket, loggerFactory.CreateLogger<TwitchPubSubClient>());

            optionsAction?.Invoke(client);

            return client;
        }
    }
}
