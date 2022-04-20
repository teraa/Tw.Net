using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Twitch
{
    public class FixedWindowRateLimiter : IRateLimiter, IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private readonly Timer _timer;
        private int _done;
        private bool _disposedValue;

        public FixedWindowRateLimiter(int limit, TimeSpan interval)
        {
            Limit = limit;
            Interval = interval;
            _sem = new(Limit, Limit);
            _timer = new()
            {
                AutoReset = false,
                Interval = Interval.TotalMilliseconds
            };
            _timer.Elapsed += Refill;
            _done = 0;
        }

        public int Limit { get; }
        public TimeSpan Interval { get; }

        private void Refill(object? sender, ElapsedEventArgs e)
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

        public async ValueTask Perform(Func<ValueTask> func, CancellationToken cancellationToken = default)
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

        public async ValueTask<T> Perform<T>(Func<ValueTask<T>> func, CancellationToken cancellationToken = default)
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
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try { _timer.Dispose(); } catch { }
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
