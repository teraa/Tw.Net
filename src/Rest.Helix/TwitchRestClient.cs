using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch.Rest.Helix
{
    public class TwitchRestClient : IDisposable
    {
        private readonly IRestClient _client;

        public TwitchRestClient(string clientId, string token, TwitchRestOptions? options = null)
        {
            if (clientId is null) throw new ArgumentNullException(nameof(clientId));
            if (token is null) throw new ArgumentNullException(nameof(token));

            options ??= new();
            _client = options.RestClientProvider(clientId, token);
        }

        public Task<GetResponse<User>?> GetUsersAsync(GetUsersArgs args, CancellationToken cancellationToken = default)
             => GetAsync<User>("users", args, cancellationToken);

        public Task<GetResponse<Video>?> GetVideosAsync(GetVideosArgs args, CancellationToken cancellationToken = default)
            => GetAsync<Video>("videos", args, cancellationToken);

        public Task<GetResponse<Follow>?> GetFollowsAsync(GetFollowsArgs args, CancellationToken cancellationToken = default)
            => GetAsync<Follow>("users/follows", args, cancellationToken);

        private Task<GetResponse<T>?> GetAsync<T>(string endpoint, IRequestArgs args, CancellationToken cancellationToken)
            => _client.SendAsync<GetResponse<T>>(HttpMethod.Get, endpoint, args, cancellationToken);

        public void Dispose()
        {
            (_client as IDisposable)?.Dispose();
        }
    }
}
