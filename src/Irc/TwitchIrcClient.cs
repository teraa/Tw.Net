using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Twitch.Irc
{
    public class TwitchIrcClient
    {
        internal const string AnonLoginPrefix = "justinfan";
        internal const string AnonLogin = AnonLoginPrefix + "1";

        private readonly TimeSpan _loginTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pongTimeout = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _eventWarningTimeout = TimeSpan.FromSeconds(2);
        private readonly ISocketClient _client;
        private readonly ILogger<TwitchIrcClient>? _logger;
        private readonly AsyncEventInvoker _eventInvoker;
        private readonly Timer _pingTimer;
        private string? _login;
        private string? _token;
        private Task? _listenerTask;

        public TwitchIrcClient(ISocketClient client, ILogger<TwitchIrcClient>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;

            _eventInvoker = new AsyncEventInvoker(_eventWarningTimeout, _logger);
            _pingTimer = new Timer();
            _pingTimer.Elapsed += PingTimerElapsed;
            _pingTimer.AutoReset = true;
        }

        #region events
        public event Func<Task>? Connected;
        public event Func<Task>? Disconnected;
        public event Func<string, Task>? RawMessageSent;
        public event Func<string, Task>? RawMessageReceived;
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

        public async Task ConnectAsync(string login, string token, CancellationToken cancellationToken = default)
        {
            _login = login ?? throw new ArgumentNullException(nameof(login));
            _token = token ?? throw new ArgumentNullException(nameof(token));

            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _listenerTask = ListenAsync(cancellationToken);

            var caps = "twitch.tv/tags twitch.tv/commands";
            if (RequestMembershipCapability)
                caps += " twitch.tv/membership";

            var capReq = new IrcMessage
            {
                Command = IrcCommand.CAP,
                Arg = "REQ",
                Content = (caps, null)
            };

            await SendAsync(capReq).ConfigureAwait(false);

            await LoginAsync(cancellationToken).ConfigureAwait(false);

            _pingTimer.Interval = _pingInterval.TotalMilliseconds;
            _pingTimer.Enabled = true;

            await _eventInvoker.InvokeAsync(Connected, nameof(Connected)).ConfigureAwait(false);
        }
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return ConnectAsync(AnonLogin, "", cancellationToken);
        }

        public async Task DisconnectAsync()
        {
            _pingTimer.Enabled = false;

            // TODO: cancel the token
            _client.Disconnect();

            if (_listenerTask is Task task)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }

            _listenerTask = null;

        }

        public async Task SendRawAsync(string message)
        {
            await _client.SendAsync(message).ConfigureAwait(false);
            await _eventInvoker.InvokeAsync(RawMessageSent, nameof(RawMessageSent), message).ConfigureAwait(false);
        }

        public async Task SendAsync(IrcMessage message)
        {
            var raw = message.ToString();
            await SendRawAsync(raw).ConfigureAwait(false);
            await _eventInvoker.InvokeAsync(IrcMessageSent, nameof(IrcMessageSent), message).ConfigureAwait(false);
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await _client.ReadAsync(cancellationToken).ConfigureAwait(false);
                    if (message is not null)
                    {
                        await HandleRawMessageAsync(message).ConfigureAwait(false);
                    }
                    // TODO: else disconnect?
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in listener task");
                // TODO: restart
            }
        }

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

        private async Task LoginAsync(CancellationToken cancellationToken)
        {
            var passReq = new IrcMessage
            {
                Command = IrcCommand.PASS,
                Content = ("oauth:" + _token, null)
            };
            await SendAsync(passReq).ConfigureAwait(false);

            var nickReq = new IrcMessage
            {
                Command = IrcCommand.NICK,
                Content = (_login!, null),
            };
            await SendAsync(nickReq).ConfigureAwait(false);

            if (!IsAnonLogin)
            {
                Func<IrcMessage, bool> predicate = x => x is { Command: IrcCommand.GLOBALUSERSTATE or IrcCommand.NOTICE };
                var response = await GetNextMessageAsync(predicate, _loginTimeout, cancellationToken).ConfigureAwait(false);

                if (response is null)
                {
                    _logger?.LogWarning("Login timed out after " + _loginTimeout.ToString());
                    // TODO: login timed out, reconnect
                }
                else if (response.Command == IrcCommand.NOTICE)
                {
                    _logger?.LogError(response.Content?.Text ?? "Login failed");
                    // TODO: login failed, disconnect
                }
                else
                {
                    // TODO: set globaluserstate data
                }
            }
        }

        private async Task HandleRawMessageAsync(string rawMessage)
        {
            await _eventInvoker.InvokeAsync(RawMessageReceived, nameof(RawMessageReceived), rawMessage).ConfigureAwait(false);

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
            try
            {
                var text = DateTimeOffset.UtcNow.ToString("u");
                var request = new IrcMessage
                {
                    Command = IrcCommand.PING,
                    Content = (Text: text, Ctcp: null)
                };

                await SendAsync(request).ConfigureAwait(false);

                // TODO: token
                var response = await GetNextMessageAsync(x => x.Command == IrcCommand.PONG && x.Content?.Text == text,
                    _loginTimeout, default).ConfigureAwait(false);

                if (response is null)
                {
                    _logger?.LogWarning($"No PONG received within {_pongTimeout}");
                    // TODO: reconnect
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in PING timer");
            }
        }
    }
}
