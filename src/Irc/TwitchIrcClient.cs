using IrcMessageParser;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Twitch.Clients;
using Timer = System.Timers.Timer;

namespace Twitch.Irc
{
    public class TwitchIrcClient : IDisposable
    {
        private readonly ISocketClient _socket;
        private readonly ILogger<TwitchIrcClient> _logger;
        private readonly Timer _pingTimer;

        public TwitchIrcClient(ISocketClient socket, ILogger<TwitchIrcClient> logger)
        {
            _socket = socket;
            _logger = logger;

            _socket.Received += SocketReceivedAsync;
            _socket.ClosedUnexpectedly += SocketClosedUnexpectedlyAsync;

            _pingTimer = new Timer();
            _pingTimer.Elapsed += PingTimerElapsed;
            _pingTimer.AutoReset = true;
        }

        #region events
        // TODO: cancel token arg?
        public event Func<ValueTask>? Connected;
        public event Func<ValueTask>? Disconnected;
        public event Func<IrcMessage, ValueTask>? IrcMessageSent;
        public event Func<IrcMessage, ValueTask>? IrcMessageReceived;
        #endregion

        #region props
        public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        public TimeSpan PingInterval { get; set; } = TimeSpan.FromMinutes(2);
        public TimeSpan PongTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan LoginTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public List<string> Capabilities { get; } = new() { "twitch.tv/tags", "twitch.tv/commands", "twitch.tv/membership" };
        #endregion

        public async ValueTask SendAsync(IrcMessage message, CancellationToken cancellationToken = default)
        {
            // TODO: Deserialize IrcMessage to bytes
            // TODO: Reuse a buffer for sending
            // TODO: Ratelimit per command type

            var rawMessage = message.ToString();
            var bytes = Encoding.GetBytes(rawMessage);

            await _socket.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
            await InvokeAsync(IrcMessageSent, nameof(IrcMessageSent), message).ConfigureAwait(false);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            // TODO: close if cancelled

            await _socket.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await SendCapabilitiesAsync(cancellationToken).ConfigureAwait(false);

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

        public Task LoginAnonAsync(CancellationToken cancellationToken = default)
        {
            return SendLoginAsync("justinfan1", "", cancellationToken);
        }

        public async Task LoginAsync(string login, string token, CancellationToken cancellationToken = default)
        {
            if (login is null) throw new ArgumentNullException(nameof(login));
            if (token is null) throw new ArgumentNullException(nameof(token));

            Func<IrcMessage, bool> predicate = x => x is { Command: IrcCommand.GLOBALUSERSTATE or IrcCommand.NOTICE };
            var responseTask = GetNextMessageAsync(predicate, LoginTimeout, cancellationToken);

            await SendLoginAsync(nick: login, pass: "oauth:" + token, cancellationToken).ConfigureAwait(false);

            var response = await responseTask.ConfigureAwait(false);

            // TODO: Close when throw?
            switch (response)
            {
                case null:
                {
                    var message = $"Login timed out after {LoginTimeout}.";
                    _logger.LogError(message);
                    throw new TimeoutException(message);
                }

                case { Command: IrcCommand.NOTICE } notice:
                {
                    var message = notice.Content?.Text ?? "Login failed.";
                    _logger.LogError(message);
                    throw new AuthenticationException(message);
                }

                case { Command: IrcCommand.GLOBALUSERSTATE } state:
                    // TODO: Set state
                    break;
            }
        }

        private async ValueTask SocketReceivedAsync(ReadOnlySequence<byte> buffer)
        {
            // TODO: optimize
            var text = Encoding.GetString(buffer);
            var message = IrcMessage.Parse(text);

            await InvokeAsync(IrcMessageReceived, nameof(IrcMessageReceived), message).ConfigureAwait(false);
        }

        private async ValueTask SocketClosedUnexpectedlyAsync(Exception? exception)
        {
            // TODO: Token
            // TODO: Delay
            await ConnectAsync().ConfigureAwait(false);
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

        private async Task SendCapabilitiesAsync(CancellationToken cancellationToken)
        {
            var message = new IrcMessage
            {
                Command = IrcCommand.CAP,
                Arg = "REQ",
                Content = new(string.Join(' ', Capabilities)),
            };

            await SendAsync(message, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendLoginAsync(string nick, string pass, CancellationToken cancellationToken)
        {
            var passMsg = new IrcMessage
            {
                Command = IrcCommand.PASS,
                Content = new(pass),
            };

            var nickMsg = new IrcMessage
            {
                Command = IrcCommand.NICK,
                Content = new(nick),
            };

            await SendAsync(passMsg, cancellationToken).ConfigureAwait(false);
            await SendAsync(nickMsg, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<IrcMessage?> GetNextMessageAsync(Func<IrcMessage, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<IrcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sourceTask = source.Task;
            Task winnerTask;
            IrcMessageReceived += Handler;
            try
            {
                winnerTask = await Task.WhenAny(Task.Delay(timeout, cancellationToken), sourceTask).ConfigureAwait(false);
            }
            finally
            {
                IrcMessageReceived -= Handler;
            }

            return winnerTask == sourceTask
                ? await sourceTask.ConfigureAwait(false)
                : null;

            ValueTask Handler(IrcMessage ircMessage)
            {
                if (predicate(ircMessage))
                    source.TrySetResult(ircMessage);

                return ValueTask.CompletedTask;
            }
        }

        private async void PingTimerElapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            ((Timer)sender).Enabled = false;
#endif
            try
            {
                var ts = DateTimeOffset.UtcNow.ToString("u");
                var request = new IrcMessage
                {
                    Command = IrcCommand.PING,
                    Content = new(ts),
                };

                // TODO: Token
                // if (_disconnectTokenSource?.Token is not CancellationToken cancellationToken)
                //     return;
                CancellationToken cancellationToken = default;

                Func<IrcMessage, bool> predicate = x => x is { Command: IrcCommand.PONG, Content: { } content } && content == request.Content;
                var responseTask = GetNextMessageAsync(predicate, PongTimeout, cancellationToken);

                await SendAsync(request, cancellationToken).ConfigureAwait(false);

                var response = await responseTask.ConfigureAwait(false);
                if (response is null)
                {
                    _logger.LogWarning($"No PONG received within {PongTimeout}.");
                    // _disconnectTokenSource?.Cancel();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in {nameof(PingTimerElapsed)}.");
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
