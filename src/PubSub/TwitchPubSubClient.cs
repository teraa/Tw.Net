using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Twitch.Clients;
using Timer = System.Timers.Timer;

namespace Twitch.PubSub
{
    public class TwitchPubSubClient : IDisposable
    {
        private readonly ISocketClient _socket;
        private ILogger<TwitchPubSubClient> _logger;
        private readonly Timer _pingTimer;

        public TwitchPubSubClient(ISocketClient socket, ILogger<TwitchPubSubClient> logger)
        {
            _socket = socket;
            _logger = logger;

            _socket.Received += SocketReceivedAsync;
            _socket.ClosedUnexpectedly += SocketClosedUnexpectedlyAsync;

            _pingTimer = new Timer();
            _pingTimer.Elapsed += PingTimerElapsed;
            _pingTimer.AutoReset = true;

            PubSubMessageReceived += HandlePubSubMessageAsync;
        }

        #region events
        public event Func<ValueTask>? Connected;
        public event Func<ValueTask>? Disconnected;
        public event Func<PubSubMessage, ValueTask>? PubSubMessageSent;
        public event Func<PubSubMessage, ValueTask>? PubSubMessageReceived;
        public event Func<Topic, ModeratorAction, ValueTask>? ModeratorActionReceived;
        #endregion events

        #region props
        public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        public TimeSpan PingInterval { get; set; } = TimeSpan.FromMinutes(2);
        public TimeSpan PongTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(5);
        #endregion

        public async ValueTask SendAsync(PubSubMessage message, CancellationToken cancellationToken = default)
        {
            // TODO: deserialize to bytes
            // TODO: Reuse buffer for sending

            var rawMessage = PubSubParser.ToJson(message);
            var bytes = Encoding.GetBytes(rawMessage);

            await _socket.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
            await InvokeAsync(PubSubMessageSent, nameof(PubSubMessageSent), message).ConfigureAwait(false);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            // TODO: close if cancelled

            await _socket.ConnectAsync(cancellationToken).ConfigureAwait(false);

            if (PingInterval > TimeSpan.Zero)
            {
                _pingTimer.Interval = PingInterval.TotalMilliseconds;
                _pingTimer.Enabled = true;
            }

            _logger.LogInformation("Connected.");

            await InvokeAsync(Connected, nameof(Connected)).ConfigureAwait(false);
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _pingTimer.Enabled = false;
            // TODO: cancel token?
            await _socket.CloseAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Disconnected.");

            await InvokeAsync(Disconnected, nameof(Disconnected)).ConfigureAwait(false);
        }

        public async Task<PubSubMessage> ListenAsync(Topic topic, string token, CancellationToken cancellationToken = default)
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

            Func<PubSubMessage, bool> predicate = x => x.Type == PubSubMessage.MessageType.RESPONSE && x.Nonce == message.Nonce;
            var responseTask = PubSubMessageReceived.GetResponseAsync(predicate, ResponseTimeout, cancellationToken);

            await SendAsync(message, cancellationToken).ConfigureAwait(false);
            var response = await responseTask.ConfigureAwait(false);

            return response;
        }

        public async Task<PubSubMessage> UnlistenAsync(Topic topic, CancellationToken cancellationToken = default)
        {
            if (topic is null) throw new ArgumentNullException(nameof(topic));

            var message = new PubSubMessage
            {
                Type = PubSubMessage.MessageType.UNLISTEN,
                Data = new PubSubMessage.MessageData
                {
                    Topics = new[] { topic }
                },
                Nonce = Guid.NewGuid().ToString()
            };

            Func<PubSubMessage, bool> predicate = x => x.Type == PubSubMessage.MessageType.RESPONSE && x.Nonce == message.Nonce;
            var responseTask = PubSubMessageReceived.GetResponseAsync(predicate, ResponseTimeout, cancellationToken);

            await SendAsync(message, cancellationToken).ConfigureAwait(false);
            var response = await responseTask.ConfigureAwait(false);

            return response;
        }

        private async ValueTask SocketReceivedAsync(ReadOnlySequence<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                // PONGs have 2x CRLF at the end.
                // One is trimmed, second is interpreted as an empty message - ignore.
                return;
            }

            // TODO: optimize
            var text = Encoding.GetString(buffer);
            var message = PubSubParser.Parse<PubSubMessage>(text);

            await InvokeAsync(PubSubMessageReceived, nameof(PubSubMessageReceived), message).ConfigureAwait(false);
        }

        private async ValueTask SocketClosedUnexpectedlyAsync(Exception? exception)
        {
            // TODO: Token
            // TODO: Delay
            await ConnectAsync().ConfigureAwait(false);
        }

        private async ValueTask HandlePubSubMessageAsync(PubSubMessage pubSubMessage)
        {
            try
            {
                if (pubSubMessage.Type != PubSubMessage.MessageType.MESSAGE)
                    return;

                var data = pubSubMessage.Data!;
                var topic = data.Topic!;
                var messageJson = data.Message!;
                var model = PubSubParser.ParseMessage(topic, messageJson);

                switch (model)
                {
                    case ModeratorAction m:
                        await InvokeAsync(ModeratorActionReceived, nameof(ModeratorActionReceived), topic, m).ConfigureAwait(false);
                        break;
                    default:
                        _logger.LogWarning($"Unhandled message: {PubSubParser.ToJson(pubSubMessage)}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception thrown while handling message: {PubSubParser.ToJson(pubSubMessage)}");
            }
        }

        private async ValueTask InvokeAsync(Func<ValueTask>? @event, string eventName)
        {
            var evt = @event;
            if (evt is null) return;

            try
            {
                await evt.Invoke().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Exception executing {eventName} handler.");
            }
        }

        private async ValueTask InvokeAsync<T>(Func<T, ValueTask>? @event, string eventName, T arg)
        {
            var evt = @event;
            if (evt is null) return;

            try
            {
                await evt.Invoke(arg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Exception executing {eventName} handler.");
            }
        }

        private async ValueTask InvokeAsync<T1, T2>(Func<T1, T2, ValueTask>? @event, string eventName, T1 arg1, T2 arg2)
        {
            var evt = @event;
            if (evt is null) return;

            try
            {
                await evt.Invoke(arg1, arg2).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Exception executing {eventName} handler.");
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

                // TODO: Token
                // if (_disconnectTokenSource?.Token is not CancellationToken cancellationToken)
                //     return;
                CancellationToken cancellationToken = default;

                Func<PubSubMessage, bool> predicate = x => x is { Type: PubSubMessage.MessageType.PONG };
                var responseTask = PubSubMessageReceived.GetResponseAsync(predicate, PongTimeout, cancellationToken);

                await SendAsync(request, cancellationToken).ConfigureAwait(false);

                await responseTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException)
            {
                _logger.LogWarning($"No PONG received within {PongTimeout}.");
                // _disconnectTokenSource?.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in PING timer");
            }
#if DEBUG
            ((Timer)sender).Enabled = true;
#endif
        }

        public void Dispose()
        {
            // TODO
            (_socket as IDisposable)?.Dispose();
            _pingTimer.Dispose();
        }
    }
}
