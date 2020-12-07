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

        private readonly TimeSpan _loginTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pongTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(1);
        private readonly Timer _pingTimer;
        private string? _login;
        private string? _token;

        public TwitchIrcClient(ISocketClient client, ILogger? logger = null)
            : base(client, logger)
        {
            _pingTimer = new Timer();
            _pingTimer.Elapsed += PingTimerElapsed;
            _pingTimer.AutoReset = true;
        }

        #region events
        public event Func<Task>? Ready;
        public event Func<IrcMessage, Task>? IrcMessageSent;
        public event Func<IrcMessage, Task>? IrcMessageReceived;
        #endregion events

        #region properties
        public TimeSpan LoginTimeout
        {
            get => _loginTimeout;
            init => _loginTimeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentOutOfRangeException(nameof(LoginTimeout));
        }

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

        public bool RequestMembershipCapability { get; init; } = false;

        private bool IsAnonLogin
            => _login?.StartsWith(AnonLoginPrefix, StringComparison.OrdinalIgnoreCase) == true;
        #endregion properties

        public async Task SendAsync(IrcMessage message)
        {
            var raw = message.ToString();
            await SendRawAsync(raw).ConfigureAwait(false);
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

            if (_pingInterval > TimeSpan.Zero)
            {
                _pingTimer.Interval = _pingInterval.TotalMilliseconds;
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
            if (RequestMembershipCapability)
                caps += " twitch.tv/membership";

            var capReq = new IrcMessage
            {
                Command = IrcCommand.CAP,
                Arg = "REQ",
                Content = (Text: caps, Ctcp: null)
            };

            await SendAsync(capReq).ConfigureAwait(false);
        }

        private async Task<bool> LoginAsync(CancellationToken cancellationToken)
        {
            var passReq = new IrcMessage
            {
                Command = IrcCommand.PASS,
                Content = (Text: "oauth:" + _token, Ctcp: null)
            };
            await SendAsync(passReq).ConfigureAwait(false);

            var nickReq = new IrcMessage
            {
                Command = IrcCommand.NICK,
                Content = (Text: _login!, Ctcp: null),
            };
            await SendAsync(nickReq).ConfigureAwait(false);

            if (!IsAnonLogin)
            {
                Func<IrcMessage, bool> predicate = x => x is { Command: IrcCommand.GLOBALUSERSTATE or IrcCommand.NOTICE };
                var response = await GetNextMessageAsync(predicate, _loginTimeout, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    _logger?.LogWarning("Login timed out after " + _loginTimeout.ToString());
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
                    Content = (Text: text, Ctcp: null)
                };

                await SendAsync(request).ConfigureAwait(false);

                if (_disconnectTokenSource?.Token is not CancellationToken cancellationToken)
                    return;

                var response = await GetNextMessageAsync(x => x.Command == IrcCommand.PONG && x.Content?.Text == text,
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
