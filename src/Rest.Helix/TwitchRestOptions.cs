using System;
using System.Net.Http;

namespace Twitch.Rest.Helix
{
    public class TwitchRestOptions
    {
        public Func<string, string, IRestClient> RestClientProvider { get; init; }
            = (clientId, token) => new RestClient
            (
                new HttpClient
                {
                    BaseAddress = new Uri("https://api.twitch.tv/helix/"),
                    DefaultRequestHeaders =
                    {
                        { "Accept", "application/vnd.twitchtv.v5+json" },
                        { "Client-ID", clientId },
                        { "Authorization", $"Bearer {token}" }
                    }
                }
            );
    }
}
