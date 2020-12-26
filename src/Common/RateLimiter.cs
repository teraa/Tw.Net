using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Twitch
{
    public class RateLimiter : IRateLimiter, IDisposable
    {
        private readonly int _size;
        private readonly SemaphoreSlim _sem;
        private readonly Timer _timer;
        private int _done;
        private bool disposedValue;

        public RateLimiter(Bucket bucket)
        {
            if (bucket is null) throw new ArgumentNullException(nameof(bucket));
            _size = bucket.Size;
            _sem = new(_size, _size);
            _timer = new()
            {
                AutoReset = false,
                Interval = bucket.RefillRate.TotalMilliseconds
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
