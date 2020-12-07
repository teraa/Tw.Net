using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Twitch
{
    public abstract class PersistentSocketClient
    {
        protected readonly AsyncEventInvoker _eventInvoker;
        protected readonly ISocketClient _client;
        protected readonly ILogger? _logger;
        protected readonly TimeSpan _eventWarningTimeout = TimeSpan.FromSeconds(2); // TODO
        protected CancellationTokenSource? _stoppingTokenSource;
        protected CancellationTokenSource? _disconnectTokenSource;
        private Task? _listenerTask;

        public PersistentSocketClient(ISocketClient client, ILogger? logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
            _eventInvoker = new AsyncEventInvoker(_eventWarningTimeout, _logger);
        }

        #region events
        public event Func<Task>? Connected;
        public event Func<Task>? Disconnected;
        public event Func<string, Task>? RawMessageSent;
        public event Func<string, Task>? RawMessageReceived;
        #endregion events

        public async Task SendRawAsync(string message)
        {
            await _client.SendAsync(message).ConfigureAwait(false);
            await _eventInvoker.InvokeAsync(RawMessageSent, nameof(RawMessageSent), message).ConfigureAwait(false);
        }
        protected async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _stoppingTokenSource = new CancellationTokenSource();
            _disconnectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingTokenSource.Token);

            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _listenerTask = ListenAsync();

            await _eventInvoker.InvokeAsync(Connected, nameof(Connected)).ConfigureAwait(false);
        }

        protected async Task DisconnectAsync()
        {
            _stoppingTokenSource?.Cancel();

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

        private async Task ListenAsync()
        {
            try
            {
                while (_disconnectTokenSource?.IsCancellationRequested == false)
                {
                    var message = await _client.ReadAsync(_disconnectTokenSource.Token).ConfigureAwait(false);
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
            }

            _disconnectTokenSource?.Cancel();

            await HandleDisconnectAsync().ConfigureAwait(false);
        }

        protected virtual Task HandleRawMessageAsync(string rawMessage)
        {
            return _eventInvoker.InvokeAsync(RawMessageReceived, nameof(RawMessageReceived), rawMessage);
        }

        protected virtual async Task HandleDisconnectAsync()
        {
            _client.Disconnect();

            await _eventInvoker.InvokeAsync(Disconnected, nameof(Disconnected)).ConfigureAwait(false);

            if (_stoppingTokenSource?.IsCancellationRequested == false)
            {
                try
                {
                    // TODO: Delay
                    await ReconnectAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception thrown while trying to reconnect");
                }
            }
        }

        protected abstract Task ReconnectAsync();
    }
}
