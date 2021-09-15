using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch.Clients
{
    public interface ISocketClient
    {
        event Func<ReadOnlySequence<byte>, ValueTask>? Received;
        event Func<Exception?, ValueTask>? ClosedUnexpectedly;

        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task CloseAsync(CancellationToken cancellationToken = default);

        ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
    }
}
