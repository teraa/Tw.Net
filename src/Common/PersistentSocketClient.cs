using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public abstract class PersistentSocketClient
    {
        protected readonly AsyncEventInvoker _eventInvoker;
        protected readonly ILogger? _logger;
        protected CancellationTokenSource? _stoppingTokenSource;
        protected CancellationTokenSource? _disconnectTokenSource;
        private readonly ISocketClient _client;
        private readonly PersistentSocketOptions _options;
        private readonly SemaphoreSlim _connectSem;
        private Task? _listenerTask;

        public PersistentSocketClient(PersistentSocketOptions options, ILogger? logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _client = _options.SocketClientProvider() ?? throw new ArgumentNullException(nameof(_client));
            _logger = logger;
            _eventInvoker = new AsyncEventInvoker(_options.HandlerWarningTimeout, _logger);
            _connectSem = new SemaphoreSlim(1, 1);
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

        protected virtual Task ConnectInternalAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
        protected virtual Task ReconnectInternalAsync()
            => ConnectAsync(default);
        protected virtual Task DisconnectInternalAsync()
            => Task.CompletedTask;
        protected virtual Task HandleRawMessageAsync(string rawMessage)
            => Task.CompletedTask;

        public virtual async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("Connecting");
            try
            {
                _stoppingTokenSource = new CancellationTokenSource();
                _disconnectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingTokenSource.Token);

                await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                _listenerTask = ListenAsync();

                _logger?.LogInformation("Connected");
                await _eventInvoker.InvokeAsync(Connected, nameof(Connected)).ConfigureAwait(false);

                await ConnectInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _connectSem.Release();
            }
        }

        public virtual async Task DisconnectAsync()
        {
            await _connectSem.WaitAsync().ConfigureAwait(false);
            _logger?.LogDebug("Disconnecting");
            try
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
            finally
            {
                _connectSem.Release();
            }
        }

        private async Task ListenAsync()
        {
            try
            {
                while (_disconnectTokenSource?.IsCancellationRequested == false)
                {
                    var message = await _client.ReadAsync(_disconnectTokenSource.Token).ConfigureAwait(false);
                    if (message is { Length: > 0 })
                    {
                        await _eventInvoker.InvokeAsync(RawMessageReceived, nameof(RawMessageReceived), message).ConfigureAwait(false);
                        await HandleRawMessageAsync(message).ConfigureAwait(false);
                    }
                    else if (message is null)
                    {
#if DEBUG
                        _logger?.LogDebug("Read message is null");
#endif
                        // End of stream, reconnect
                        break;
                    }
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

        private async Task HandleDisconnectAsync()
        {
            await DisconnectInternalAsync().ConfigureAwait(false);
            _client.Disconnect();

            _logger?.LogInformation("Disconnected");
            await _eventInvoker.InvokeAsync(Disconnected, nameof(Disconnected)).ConfigureAwait(false);

            if (_stoppingTokenSource?.IsCancellationRequested == false)
            {
                try
                {
                    await Task.Delay(5000); // TODO
                    await ReconnectInternalAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception thrown while trying to reconnect");
                }
            }
        }

    }
}
