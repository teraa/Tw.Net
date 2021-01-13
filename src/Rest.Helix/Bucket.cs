using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch.Rest.Helix
{
    internal class Bucket : IDisposable
    {
        private readonly TimeSpan s_period = TimeSpan.FromSeconds(60);

        private readonly string _limitHeader;
        private readonly string _remainingHeader;
        private readonly SemaphoreSlim _sem;
        private readonly object _headersLock;
        private int _limit;
        private int _remaining;
        private DateTimeOffset _last;

        public Bucket(int initialLimit, string limitHeader, string remainingHeader)
        {
            _limit = initialLimit;
            _remaining = initialLimit;
            _limitHeader = limitHeader ?? throw new ArgumentNullException(nameof(limitHeader));
            _remainingHeader = remainingHeader ?? throw new ArgumentNullException(nameof(remainingHeader));
            _sem = new SemaphoreSlim(1, 1);
            _headersLock = new object();
        }

        public void Dispose()
        {
            ((IDisposable)_sem).Dispose();
        }

        public void Update(HttpHeaders headers)
        {
            lock (_headersLock)
            {
                if (TryGetValue(_limitHeader, out var limit))
                    _limit = int.Parse(limit);
                if (TryGetValue(_remainingHeader, out var remaining))
                    _remaining = int.Parse(remaining);
            }

            bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
            {
                if (headers.TryGetValues(key, out var values) && values.Any())
                    value = values.First();
                else
                    value = null;

                return value is not null;
            }
        }

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            await _sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnterAsync(cancellationToken).ConfigureAwait(false);
                _last = DateTimeOffset.UtcNow;
            }
            finally
            {
                _sem.Release();
            }
        }

        private Task EnterAsync(CancellationToken cancellationToken)
        {
            lock (_headersLock)
            {
                if (_remaining > 0)
                {
                    _remaining--;
                }
                else
                {
                    TimeSpan point = s_period / _limit;
                    TimeSpan elapsed = (DateTimeOffset.UtcNow - _last);

                    if (point > elapsed)
                        return Task.Delay(point - elapsed);

                    int refill = (int)Math.Floor(elapsed / point) - 1;
                    _remaining = refill;

                    if (_remaining > _limit)
                        _remaining = _limit;
                }

                return Task.CompletedTask;
            }
        }
    }
}
