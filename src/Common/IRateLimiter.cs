using System;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public interface IRateLimiter
    {
        Task Perform(Func<Task> func, CancellationToken cancellationToken);
        Task<T> Perform<T>(Func<Task<T>> func, CancellationToken cancellationToken);
        ValueTask Perform(Func<ValueTask> func, CancellationToken cancellationToken);
        ValueTask<T> Perform<T>(Func<ValueTask<T>> func, CancellationToken cancellationToken);
    }
}
