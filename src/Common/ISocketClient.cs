using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public interface ISocketClient
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        Task DisconnectAsync(CancellationToken cancellationToken);
        Task SendAsync(string message, CancellationToken cancellationToken);
        Task<string?> ReadAsync(CancellationToken cancellationToken);
    }
}
