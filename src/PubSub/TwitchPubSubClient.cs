using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Twitch.PubSub
{
    public class TwitchPubSubClient : PersistentSocketClient
    {
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(1);
        private readonly Timer _pingTimer;

        public TwitchPubSubClient(ISocketClient client, ILogger? logger)
            : base(client, logger)
        {
            _pingTimer = new Timer();
            _pingTimer.Elapsed += PingTimerElapsed;
            _pingTimer.AutoReset = true;
        }

        public TimeSpan PingInterval
        {
            get => _pingInterval;
            init => _pingInterval = value >= TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PingInterval));
        }

        protected override Task ConnectInternalAsync(CancellationToken cancellationToken)
        {
            if (_pingInterval > TimeSpan.Zero)
            {
                _pingTimer.Interval = _pingInterval.TotalMilliseconds;
                _pingTimer.Enabled = true;
            }

            return Task.CompletedTask;
        }

        protected override Task DisconnectInternalAsync()
        {
            _pingTimer.Enabled = false;
            return Task.CompletedTask;
        }

        private async void PingTimerElapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            ((Timer)sender).Enabled = false;
#endif
            try
            {
                await SendRawAsync("{\"type\":\"PING\"}").ConfigureAwait(false);

                if (_disconnectTokenSource?.Token is not CancellationToken cancellationToken)
                    return;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in PING timer");
            }
#if DEBUG
            ((Timer)sender).Enabled = true;
#endif
        }
    }
}
