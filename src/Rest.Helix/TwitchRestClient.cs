using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch.Rest.Helix
{
    public class TwitchRestClient
    {
        private readonly IRestClient _client;

        public TwitchRestClient(string clientId, string token, TwitchRestOptions? options = null)
        {
            if (clientId is null) throw new ArgumentNullException(nameof(clientId));
            if (token is null) throw new ArgumentNullException(nameof(token));

            options ??= new();
            _client = options.RestClientProvider(clientId, token);
        }

        internal static string ToQuery(IRequestArgs args)
        {
            var query = new StringBuilder();
            var properties = args.GetType().GetProperties();

            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];

                var attributes = property.GetCustomAttributes(typeof(QueryParamAttribute), false);
                if (attributes.Length != 1) continue;

                var value = property.GetValue(args);
                if (value is null) continue;

                var name = ((QueryParamAttribute)attributes[0]).Name;

                if (value is string str)
                {
                    AppendPair(name, str);
                }
                else if (value is IEnumerable e)
                {
                    foreach (var v in e)
                    {
                        AppendPair(name, v.ToString()!);
                    }
                }
                else
                {
                    string valueStr = value switch
                    {
                        Enum en => en.ToString().ToLowerInvariant(),
                        DateTimeOffset dto => dto.ToString("o"),
                        _ => value.ToString()!
                    };

                    AppendPair(name, valueStr);
                }

            }

            query.Remove(query.Length - 1, 1); // Remove trailing &

            return query.ToString();

            void AppendPair(string key, string value)
            {
                query
                    .Append(key)
                    .Append('=')
                    .Append(Uri.EscapeDataString(value))
                    .Append('&');
            }
        }

        public Task<GetResponse<User>?> GetUsersAsync(GetUsersArgs args, CancellationToken cancellationToken = default)
             => GetAsync<User>("users", args, cancellationToken);

        public Task<GetResponse<Video>?> GetVideosAsync(GetVideosArgs args, CancellationToken cancellationToken = default)
            => GetAsync<Video>("videos", args, cancellationToken);

        public Task<GetResponse<Follow>?> GetFollowsAsync(GetFollowsArgs args, CancellationToken cancellationToken = default)
            => GetAsync<Follow>("users/follows", args, cancellationToken);

        private async Task<GetResponse<T>?> GetAsync<T>(string endpoint, IRequestArgs args, CancellationToken cancellationToken)
        {
            var requestUri = endpoint;

            if (args is not null)
                requestUri += '?' + ToQuery(args);

            return await _client.SendAsync<GetResponse<T>>(HttpMethod.Get, requestUri, cancellationToken);
        }
    }
}
