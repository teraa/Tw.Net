using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Teraa.Irc;
using Twitch.Clients;
using Timer = System.Timers.Timer;

namespace Twitch.Irc
{
    public class TwitchIrcClient : TwitchIrcClientOptions, IDisposable
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

        public TwitchIrcClient(ISocketClient socket, ILoggerFactory loggerFactory)
            : this(socket, loggerFactory.CreateLogger<TwitchIrcClient>()) { }

        #region events
        // TODO: cancel token arg?
        public event Func<ValueTask>? Connected;
        public event Func<ValueTask>? Disconnected;
        public event Func<Message, ValueTask>? IrcMessageSent;
        public event Func<Message, ValueTask>? IrcMessageReceived;
        #endregion

        #region props
        public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        public TimeSpan PingInterval { get; set; } = TimeSpan.FromMinutes(2);
        public TimeSpan PongTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan LoginTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public List<string> Capabilities { get; } = new() { "twitch.tv/tags", "twitch.tv/commands", "twitch.tv/membership" };
        public IRateLimiter JoinLimiter { get; set; } = new SlidingWindowRateLimiter(20, TimeSpan.FromSeconds(10));
        public IRateLimiter CommandLimiter { get; set; } = new SlidingWindowRateLimiter(20, TimeSpan.FromSeconds(30));
        #endregion

        public async ValueTask SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            string rawMessage = message.ToString();
            int length = Encoding.GetByteCount(rawMessage);

            using (IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(length))
            {
                Memory<byte> memory = owner.Memory.Slice(0, length);
                Encoding.GetBytes(rawMessage, memory.Span);

                var limiter = message.Command switch
                {
                    Command.JOIN => JoinLimiter,
                    Command.PRIVMSG => CommandLimiter,
                    _ => null,
                };

                if (limiter is null)
                {
                    await _socket.SendAsync(memory, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await limiter.Perform(() => _socket.SendAsync(memory, cancellationToken), cancellationToken).ConfigureAwait(false);
                }
            }

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

            Func<Message, bool> predicate = x => x is { Command: Command.GLOBALUSERSTATE or Command.NOTICE };
            var responseTask = GetResponseAsync(predicate, LoginTimeout, cancellationToken);

            await SendLoginAsync(nick: login, pass: "oauth:" + token, cancellationToken).ConfigureAwait(false);

            Message response;
            try
            {
                response = await responseTask.ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                var message = $"Login timed out after {LoginTimeout}.";
                _logger.LogError(message);
                // TODO: Close when throw?
                throw;
            }

            // TODO: Close when throw?
            switch (response.Command)
            {
                case Command.NOTICE:
                    {
                        var message = response.Content?.Text ?? "Login failed.";
                        _logger.LogError(message);
                        throw new AuthenticationException(message);
                    }

                case Command.GLOBALUSERSTATE:
                    // TODO: Set state
                    break;
            }
        }

        private async ValueTask SocketReceivedAsync(ReadOnlySequence<byte> buffer)
        {
            // TODO: optimize
            var text = Encoding.GetString(buffer);
            var message = Message.Parse(text);

            await InvokeAsync(IrcMessageReceived, nameof(IrcMessageReceived), message).ConfigureAwait(false);
        }

        private async ValueTask SocketClosedUnexpectedlyAsync(Exception? exception)
        {
            // TODO: Token
            CancellationToken cancellationToken = default;

            int delaySeconds = 0;
            while (true)
            {
                try
                {
                    await Task.Delay(delaySeconds * 1000, cancellationToken).ConfigureAwait(false);
                    await ConnectAsync(cancellationToken).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    delaySeconds = delaySeconds switch
                    {
                        0 => 1,
                        <= 64 => delaySeconds * 2,
                        _ => delaySeconds,
                    };

                    _logger.LogError(ex, $"Exception while reconnecting.");
                }
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

        private async Task SendCapabilitiesAsync(CancellationToken cancellationToken)
        {
            var message = new Message
            {
                Command = Command.CAP,
                Arg = "REQ",
                Content = new(string.Join(' ', Capabilities)),
            };

            await SendAsync(message, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendLoginAsync(string nick, string pass, CancellationToken cancellationToken)
        {
            var passMsg = new Message
            {
                Command = Command.PASS,
                Content = new(pass),
            };

            var nickMsg = new Message
            {
                Command = Command.NICK,
                Content = new(nick),
            };

            await SendAsync(passMsg, cancellationToken).ConfigureAwait(false);
            await SendAsync(nickMsg, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<Message> GetResponseAsync(Func<Message, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
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

            if (winnerTask == sourceTask)
                return await sourceTask.ConfigureAwait(false);

            throw new TimeoutException($"Response not received within {timeout}.");

            ValueTask Handler(Message message)
            {
                if (predicate(message))
                    source.TrySetResult(message);

                return ValueTask.CompletedTask;
            }
        }

        private async void PingTimerElapsed(object? sender, ElapsedEventArgs e)
        {
#if DEBUG
            ((Timer)sender!).Enabled = false;
#endif
            try
            {
                var ts = DateTimeOffset.UtcNow.ToString("u");
                var request = new Message
                {
                    Command = Command.PING,
                    Content = new(ts),
                };

                // TODO: Token
                // if (_disconnectTokenSource?.Token is not CancellationToken cancellationToken)
                //     return;
                CancellationToken cancellationToken = default;

                Func<Message, bool> predicate = x => x is { Command: Command.PONG, Content: { } content } && content == request.Content;
                var responseTask = GetResponseAsync(predicate, PongTimeout, cancellationToken);

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
            (JoinLimiter as IDisposable)?.Dispose();
            (CommandLimiter as IDisposable)?.Dispose();
        }
    }
}
