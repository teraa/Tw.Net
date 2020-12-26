using System;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public interface IRateLimiter
    {
        Task Perform(Func<Task> func, CancellationToken cancellationToken);
        Task<T> Perform<T>(Func<Task<T>> func, CancellationToken cancellationToken);
    }
}
