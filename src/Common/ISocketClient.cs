using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public interface ISocketClient
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        void Disconnect();
        Task SendAsync(string message);
        Task<string?> ReadAsync(CancellationToken cancellationToken);
    }
}
