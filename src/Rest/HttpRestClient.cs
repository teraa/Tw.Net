using System;
using System.Collections;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch.Rest
{
    public class HttpRestClient : IRestClient, IDisposable
    {
        private static readonly JsonSerializerOptions s_options;

        static HttpRestClient()
        {
            s_options = new JsonSerializerOptions
            {
            };
        }

        private readonly HttpClient _client;

        public HttpRestClient(HttpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void Dispose()
        {
            ((IDisposable)_client).Dispose();
        }

        public async Task<TResponse?> SendAsync<TResponse>(HttpMethod httpMethod, string requestUri, CancellationToken cancellationToken)
            where TResponse : class
        {
            var request = new HttpRequestMessage(httpMethod, requestUri);

            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            TResponse? result;

            if (response.IsSuccessStatusCode)
            {
                result = await response.Content.ReadFromJsonAsync<TResponse>(s_options, cancellationToken);
            }
            else
            {
                result = null;
            }

            return result;
        }
    }
}
