using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Twitch.PubSub.Messages;
using Timer = System.Timers.Timer;

namespace Twitch.PubSub
{
    public class TwitchPubSubClient : PersistentSocketClient
    {
        private readonly TimeSpan _pongTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _responseTimeout = TimeSpan.FromSeconds(5);
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
        public event Func<Topic, ModeratorAction, Task>? ModeratorActionReceived;
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

        public TimeSpan ResponseTimeout
        {
            get => _responseTimeout;
            init => _responseTimeout = value >= TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(ResponseTimeout));
        }
        #endregion properties

        public async Task SendAsync(PubSubMessage message)
        {
            var raw = PubSubParser.ToJson(message);
            await SendRawAsync(raw).ConfigureAwait(false);
            await _eventInvoker.InvokeAsync(PubSubMessageSent, nameof(PubSubMessageSent), message).ConfigureAwait(false);
        }

        public async Task<PubSubMessage?> ListenAsync(Topic topic, string token, CancellationToken cancellationToken = default)
        {
            if (topic is null) throw new ArgumentNullException(nameof(topic));
            if (token is null) throw new ArgumentNullException(nameof(token));

            var message = new PubSubMessage
            {
                Type = PubSubMessage.MessageType.LISTEN,
                Data = new PubSubMessage.MessageData
                {
                    Topics = new[] { topic },
                    AuthToken = token
                },
                Nonce = Guid.NewGuid().ToString()
            };

            await SendAsync(message).ConfigureAwait(false);
            var response = await GetNextMessageAsync(x => x.Type == PubSubMessage.MessageType.RESPONSE && x.Nonce == message.Nonce,
                _responseTimeout, cancellationToken).ConfigureAwait(false);

            return response;
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
                await HandlePubSubMessageAsync(rawMessage, pubSubMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception thrown while parsing PubSub message: {rawMessage}");
            }
        }
        #endregion overrides

        private async Task HandlePubSubMessageAsync(string rawMessage, PubSubMessage pubSubMessage)
        {
            await _eventInvoker.InvokeAsync(PubSubMessageReceived, nameof(PubSubMessageReceived), pubSubMessage).ConfigureAwait(false);

            if (pubSubMessage.Type != PubSubMessage.MessageType.MESSAGE)
                return;

            try
            {
                var topic = pubSubMessage.Data!.Topic!;
                switch (topic.Name)
                {
                    case "chat_moderator_actions":
                        {
                            var message = PubSubParser.Parse<ChatModeratorActionsMessage>(pubSubMessage.Data.Message!);
                            var model = ModeratorAction.Create(topic, message);
                            await _eventInvoker.InvokeAsync(ModeratorActionReceived, nameof(ModeratorActionReceived), topic, model);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception thrown while handling message: {rawMessage}");
            }
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
