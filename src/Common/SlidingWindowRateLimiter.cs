using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public class SlidingWindowRateLimiter : IRateLimiter, IDisposable
    {
        private readonly LimitInfo _limit;
        private readonly SemaphoreSlim _sem;
        private Queue<DateTimeOffset> _times;
        private bool disposedValue;

        public SlidingWindowRateLimiter(LimitInfo limit)
        {
            _limit = limit ?? throw new ArgumentNullException(nameof(limit));
            _sem = new(1, 1);
            _times = new();
        }

        private async Task EnterAsync(CancellationToken cancellationToken)
        {
            await _sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_times.Count >= _limit.Count)
                {
                    var diff = DateTimeOffset.Now - _times.Peek();
                    if (diff < _limit.Interval)
                        await Task.Delay(_limit.Interval - diff, cancellationToken).ConfigureAwait(false);

                    _times.Dequeue();
                }

                _times.Enqueue(DateTimeOffset.Now);
            }
            finally
            {
                _sem.Release();
            }
        }

        public async Task Perform(Func<Task> func, CancellationToken cancellationToken = default)
        {
            await EnterAsync(cancellationToken).ConfigureAwait(false);
            await func().ConfigureAwait(false);
        }

        public async Task<T> Perform<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
        {
            await EnterAsync(cancellationToken).ConfigureAwait(false);
            return await func().ConfigureAwait(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _sem.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
