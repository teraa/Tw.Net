using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch.Rest.Helix
{
    public class RestClient : IRestClient, IDisposable
    {
        private static readonly JsonSerializerOptions s_options;

        static RestClient()
        {
            s_options = new JsonSerializerOptions();
        }

        private readonly HttpClient _client;
        private readonly SemaphoreSlim _sem;
        private readonly Bucket _globalBucket;
        private readonly IReadOnlyDictionary<(HttpMethod, string), Bucket> _buckets;
        private bool _disposedValue;

        public RestClient(HttpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _sem = new SemaphoreSlim(1, 1);

            _globalBucket = new Bucket(800, "Ratelimit-Limit", "Ratelimit-Remaining");
            _buckets = new Dictionary<(HttpMethod, string), Bucket>()
            {
                // If first request fails, subsequent request will need to wait
                // 60 / 1 = 60 seconds before 1 point is refilled. TODO: fix
                [(HttpMethod.Post, "helix/clips")] = new Bucket(1, "Ratelimit-Helixclipscreation-Limit", "Ratelimit-Helixclipscreation-Remaining")
            };
        }

        internal static string RequestArgsToQuery(IRequestArgs args)
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

        public async Task<TResponse?> SendAsync<TResponse>(HttpMethod httpMethod, string endpoint, IRequestArgs? args, CancellationToken cancellationToken)
            where TResponse : class
        {
            var requestUri = endpoint;
            if (args is not null)
                requestUri += '?' + RequestArgsToQuery(args);

            using var request = new HttpRequestMessage(httpMethod, requestUri);

            if (_buckets.TryGetValue((httpMethod, endpoint), out var bucket))
                await bucket.WaitAsync(cancellationToken).ConfigureAwait(false);

            await _globalBucket.WaitAsync(cancellationToken).ConfigureAwait(false);

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (bucket is not null)
                bucket.Update(response.Headers);

            _globalBucket.Update(response.Headers);


            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<TResponse>(s_options, cancellationToken).ConfigureAwait(false);

            try
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(s_options, cancellationToken).ConfigureAwait(false);
                var reason = errorResponse?.Message;
                if (reason is null)
                    throw new HttpException(response.StatusCode);
                else
                    throw new HttpException(response.StatusCode, reason);
            }
            catch
            {
                throw new HttpException(response.StatusCode);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try { _client.Dispose(); } catch { }
                    try { _sem.Dispose(); } catch { }
                    try { _globalBucket.Dispose(); } catch { }

                    foreach (var bucket in _buckets.Values)
                        try { bucket.Dispose(); } catch { }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
