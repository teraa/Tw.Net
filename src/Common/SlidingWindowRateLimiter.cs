using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public class SlidingWindowRateLimiter : IRateLimiter, IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private Queue<DateTimeOffset> _times;
        private bool _disposedValue;

        public SlidingWindowRateLimiter(int limit, TimeSpan interval)
        {
            Limit = limit;
            Interval = interval;
            _sem = new(1, 1);
            _times = new();
        }

        public int Limit { get; }
        public TimeSpan Interval { get; }

        private async Task EnterAsync(CancellationToken cancellationToken)
        {
            await _sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_times.Count >= Limit)
                {
                    var delay = Interval - (DateTimeOffset.Now - _times.Peek());
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

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
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try { _sem.Dispose(); } catch { }
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
