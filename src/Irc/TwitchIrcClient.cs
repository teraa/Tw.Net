using IrcMessageParser;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Twitch.Irc
{
    public class TwitchIrcClient : PersistentSocketClient
    {
        internal const string AnonLoginPrefix = "justinfan";
        internal const string AnonLogin = AnonLoginPrefix + "1";

        private readonly TwitchIrcOptions _options;
        private readonly Timer _pingTimer;
        private readonly IRateLimiter _joinLimiter, _commandLimiter;
        private string? _login;
        private string? _token;

        public TwitchIrcClient(ILogger<TwitchIrcClient>? logger = null)
            : this(new(), logger) { }

        public TwitchIrcClient(TwitchIrcOptions options, ILogger<TwitchIrcClient>? logger = null)
            : base(options, logger)
        {
            _options = options;
            _pingTimer = new Timer();
            _pingTimer.Elapsed += PingTimerElapsed;
            _pingTimer.AutoReset = true;
            _joinLimiter = new RateLimiter(_options.JoinBucket);
            _commandLimiter = new RateLimiter(_options.CommandBucket);
        }

        #region events
        public event Func<Task>? Ready;
        public event Func<IrcMessage, Task>? IrcMessageSent;
        public event Func<IrcMessage, Task>? IrcMessageReceived;
        #endregion events

        private bool IsAnonLogin
            => _login?.StartsWith(AnonLoginPrefix, StringComparison.OrdinalIgnoreCase) == true;

        public async Task SendAsync(IrcMessage message, CancellationToken cancellationToken = default)
        {
            var raw = message.ToString();

            var limiter = message.Command switch
            {
                IrcCommand.JOIN => _joinLimiter,
                IrcCommand.PRIVMSG => _commandLimiter,
                _ => null
            };

            if (limiter is null)
                await SendRawAsync(raw).ConfigureAwait(false);
            else
                await limiter.Perform(() => SendRawAsync(raw), cancellationToken).ConfigureAwait(false);

            await _eventInvoker.InvokeAsync(IrcMessageSent, nameof(IrcMessageSent), message).ConfigureAwait(false);
        }

        public override Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return ConnectAsync(AnonLogin, "", cancellationToken);
        }
        public Task ConnectAsync(string login, string token, CancellationToken cancellationToken = default)
        {
            _login = login ?? throw new ArgumentNullException(nameof(login));
            _token = token ?? throw new ArgumentNullException(nameof(token));
            return base.ConnectAsync(cancellationToken);
        }

        #region overrides
        protected override async Task ConnectInternalAsync(CancellationToken cancellationToken)
        {
            await RequestCapabilitiesAsync().ConfigureAwait(false);

            if (!await LoginAsync(cancellationToken).ConfigureAwait(false))
                return;

            if (_options.PingInterval > TimeSpan.Zero)
            {
                _pingTimer.Interval = _options.PingInterval.TotalMilliseconds;
                _pingTimer.Enabled = true;
            }

            await _eventInvoker.InvokeAsync(Ready, nameof(Ready)).ConfigureAwait(false);
        }

        protected override Task ReconnectInternalAsync()
        {
            return ConnectAsync(_login!, _token!);
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
                var ircMessage = IrcMessage.Parse(rawMessage);
                await HandleIrcMessageAsync(ircMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception thrown while parsing IRC message: {rawMessage}");
            }
        }
        #endregion overrides

        private async Task<IrcMessage?> GetNextMessageAsync(Func<IrcMessage, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<IrcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sourceTask = source.Task;
            Task winnerTask;
            IrcMessageReceived += Handler;
            try { winnerTask = await Task.WhenAny(Task.Delay(timeout, cancellationToken), sourceTask).ConfigureAwait(false); }
            finally { IrcMessageReceived -= Handler; }

            return winnerTask == sourceTask
                ? await sourceTask.ConfigureAwait(false)
                : null;

            Task Handler(IrcMessage ircMessage)
            {
                if (predicate(ircMessage))
                    source.TrySetResult(ircMessage);

                return Task.CompletedTask;
            }
        }

        private async Task RequestCapabilitiesAsync()
        {
            var caps = "twitch.tv/tags twitch.tv/commands";
            if (_options.RequestMembershipCapability)
                caps += " twitch.tv/membership";

            var capReq = new IrcMessage
            {
                Command = IrcCommand.CAP,
                Arg = "REQ",
                Content = new(caps)
            };

            await SendAsync(capReq).ConfigureAwait(false);
        }

        private async Task<bool> LoginAsync(CancellationToken cancellationToken)
        {
            var passReq = new IrcMessage
            {
                Command = IrcCommand.PASS,
                Content = new("oauth:" + _token)
            };
            await SendAsync(passReq, cancellationToken).ConfigureAwait(false);

            var nickReq = new IrcMessage
            {
                Command = IrcCommand.NICK,
                Content = new(_login!)
            };
            await SendAsync(nickReq, cancellationToken).ConfigureAwait(false);

            if (!IsAnonLogin)
            {
                Func<IrcMessage, bool> predicate = x => x is { Command: IrcCommand.GLOBALUSERSTATE or IrcCommand.NOTICE };
                var response = await GetNextMessageAsync(predicate, _options.LoginTimeout, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    _logger?.LogWarning("Login timed out after " + _options.LoginTimeout.ToString());
                    _disconnectTokenSource?.Cancel();
                    return false;
                }
                else if (response.Command == IrcCommand.NOTICE)
                {
                    throw new AuthenticationException(response.Content?.Text ?? "Login failed");
                }
                else
                {
                    // TODO: Set GLOBALUSERSTATE data
                }
            }

            return true;
        }

        private async Task HandleIrcMessageAsync(IrcMessage ircMessage)
        {
            await _eventInvoker.InvokeAsync(IrcMessageReceived, nameof(IrcMessageReceived), ircMessage).ConfigureAwait(false);

            try
            {
                switch (ircMessage.Command)
                {
                    case IrcCommand.PING:
                        var pingResponse = new IrcMessage
                        {
                            Command = IrcCommand.PONG,
                            Content = ircMessage.Content
                        };
                        await SendAsync(pingResponse).ConfigureAwait(false);
                        break;

                    // TODO
                    case IrcCommand.PRIVMSG:
                        break;

                    case IrcCommand.USERNOTICE:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception thrown while handling message: {ircMessage}");
            }
        }

        private async void PingTimerElapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            ((Timer)sender).Enabled = false;
#endif
            try
            {
                var text = DateTimeOffset.UtcNow.ToString("u");
                var request = new IrcMessage
                {
                    Command = IrcCommand.PING,
                    Content = new(text)
                };

                await SendAsync(request).ConfigureAwait(false);

                if (_disconnectTokenSource?.Token is not CancellationToken cancellationToken)
                    return;

                var response = await GetNextMessageAsync(x => x.Command == IrcCommand.PONG && x.Content?.Text == text,
                    _options.PongTimeout, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    _logger?.LogWarning($"No PONG received within {_options.PongTimeout}");
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
