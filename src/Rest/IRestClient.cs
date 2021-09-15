using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch.Rest
{
    public interface IRestClient
    {
        Task<TResponse> SendAsync<TResponse>(HttpMethod httpMethod, string endpoint, IRequestArgs? args, CancellationToken cancellationToken)
            where TResponse : class;
    }
}
