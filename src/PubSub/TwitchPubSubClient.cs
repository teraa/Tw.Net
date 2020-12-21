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
        private readonly TimeSpan _pongTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(1);
        private readonly Timer _pingTimer;

        public TwitchPubSubClient(ISocketClient client, ILogger? logger)
            : base(client, logger)
        {
            _pingTimer = new Timer();
            _pingTimer.Elapsed += PingTimerElapsed;
            _pingTimer.AutoReset = true;
        }

        #region events
        public event Func<PubSubMessage, Task>? PubSubMessageSent;
        public event Func<PubSubMessage, Task>? PubSubMessageReceived;
        #endregion events

        #region properties
        public TimeSpan PongTimeout
        {
            get => _pongTimeout;
            init => _pongTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PongTimeout));
        }

        public TimeSpan PingInterval
        {
            get => _pingInterval;
            init => _pingInterval = value >= TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PingInterval));
        }
        #endregion properties

        public async Task SendAsync(PubSubMessage message)
        {
            var raw = PubSubParser.ToJson(message);
            await SendRawAsync(raw).ConfigureAwait(false);
            await _eventInvoker.InvokeAsync(PubSubMessageSent, nameof(PubSubMessageSent), message).ConfigureAwait(false);
        }

        #region overrides
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

        protected override async Task HandleRawMessageAsync(string rawMessage)
        {
            try
            {
                var pubSubMessage = PubSubParser.Parse<PubSubMessage>(rawMessage);
                await HandlePubSubMessageAsync(pubSubMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception thrown while parsing PubSub message: {rawMessage}");
            }
        }
        #endregion overrides

        private async Task HandlePubSubMessageAsync(PubSubMessage pubSubMessage)
        {
            await _eventInvoker.InvokeAsync(PubSubMessageReceived, nameof(PubSubMessageReceived), pubSubMessage).ConfigureAwait(false);

            // TODO
        }

        // TODO: duplicate code in IRC client
        private async Task<PubSubMessage?> GetNextMessageAsync(Func<PubSubMessage, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<PubSubMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sourceTask = source.Task;
            Task winnerTask;
            PubSubMessageReceived += Handler;
            try { winnerTask = await Task.WhenAny(Task.Delay(timeout, cancellationToken), sourceTask).ConfigureAwait(false); }
            finally { PubSubMessageReceived -= Handler; }

            return winnerTask == sourceTask
                ? await sourceTask.ConfigureAwait(false)
                : null;

            Task Handler(PubSubMessage PubSubMessage)
            {
                if (predicate(PubSubMessage))
                    source.TrySetResult(PubSubMessage);

                return Task.CompletedTask;
            }
        }

private async void PingTimerElapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            ((Timer)sender).Enabled = false;
#endif
            try
            {
                var request = new PubSubMessage
                {
                    Type = PubSubMessage.MessageType.PING
                };

                await SendAsync(request).ConfigureAwait(false);

                if (_disconnectTokenSource?.Token is not CancellationToken cancellationToken)
                    return;

                var response = await GetNextMessageAsync(x => x.Type == PubSubMessage.MessageType.PONG,
                    _pongTimeout, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    _logger?.LogWarning($"No PONG received within {_pongTimeout}");
                    _disconnectTokenSource?.Cancel();
                }
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
