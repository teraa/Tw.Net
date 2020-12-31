using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Twitch
{
    public class FixedWindowRateLimiter : IRateLimiter, IDisposable
    {
        private readonly LimitInfo _limit;
        private readonly SemaphoreSlim _sem;
        private readonly Timer _timer;
        private int _done;
        private bool disposedValue;

        public FixedWindowRateLimiter(LimitInfo limit)
        {
            _limit = limit ?? throw new ArgumentNullException(nameof(limit));
            _sem = new(_limit.Count, _limit.Count);
            _timer = new()
            {
                AutoReset = false,
                Interval = _limit.Interval.TotalMilliseconds
            };
            _timer.Elapsed += Refill;
            _done = 0;
        }

        private void Refill(object sender, ElapsedEventArgs e)
        {
            _sem.Release(Interlocked.Exchange(ref _done, 0));
        }

        public async Task Perform(Func<Task> func, CancellationToken cancellationToken = default)
        {
            await _sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await func().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Increment(ref _done);
                _timer.Enabled = true;
            }
        }

        public async Task<T> Perform<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
        {
            await _sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await func().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Increment(ref _done);
                _timer.Enabled = true;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _timer.Dispose();
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
