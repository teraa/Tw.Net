using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Twitch.Clients
{

    public class WebSocketClient : ISocketClient, IDisposable
    {
        private readonly ILogger<WebSocketClient> _logger;
        private readonly Pipe _pipe;
        private readonly SemaphoreSlim _connectSem;
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _stoppingToken;
        private Task? _fillTask, _readTask;
        private State _state;

        public WebSocketClient(Uri uri, ILogger<WebSocketClient> logger)
        {
            Uri = uri;
            _logger = logger;
            _pipe = new Pipe();
            _connectSem = new SemaphoreSlim(1, 1);
            _state = State.None;
        }

        public event Func<ReadOnlySequence<byte>, ValueTask>? Received;
        public event Func<Exception?, ValueTask>? ClosedUnexpectedly;

        private enum State
        {
            None,
            Open,
            CloseRequested,
            Closed,
        }

        public Uri Uri { get; set; }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state == State.Open)
                    throw new InvalidOperationException("Client was already connected.");

                _stoppingToken = new CancellationTokenSource();

                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(Uri, cancellationToken).ConfigureAwait(false);
                _state = State.Open;

                _fillTask = FillPipeAsync(_ws, _pipe.Writer, _stoppingToken.Token);
                _readTask = ReadPipeAsync(_pipe.Reader);
            }
            finally
            {
                _connectSem.Release();
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state != State.Open) return;

                _state = State.CloseRequested;
                _stoppingToken!.CancelAfter(TimeSpan.FromSeconds(1));

                if (_ws!.State != WebSocketState.Aborted)
                {
                    try
                    {
                        await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, default, cancellationToken);
                    }
                    catch { }
                }

                await _fillTask!.ConfigureAwait(false);
                _fillTask = null;

                await _readTask!.ConfigureAwait(false);
                _readTask = null;

                _pipe.Reset();

                _stoppingToken.Dispose();
                _stoppingToken = null;

                _ws.Dispose();
                _ws = null;

                _state = State.Closed;
            }
            finally
            {
                _connectSem.Release();
            }
        }

        private async Task UnexpectedCloseAsync(Exception? exception, CancellationToken cancellationToken = default)
        {
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state != State.Open) return;

                _ws!.Abort();
                _ws.Dispose();
                _ws = null;

                _stoppingToken!.Cancel();
                _stoppingToken.Dispose();
                _stoppingToken = null;

                await _readTask!.ConfigureAwait(false);
                _readTask = null;

                _pipe.Reset();

                _state = State.Closed;
            }
            finally
            {
                _connectSem.Release();
            }

            ClosedUnexpectedly?.Invoke(exception);
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_state != State.Open)
                throw new InvalidOperationException($"Cannot send while not open. Current state: {_state}");

            await _ws!.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }

        private async Task FillPipeAsync(ClientWebSocket ws, PipeWriter writer, CancellationToken cancellationToken)
        {
            const int minimumBufferSize = 512; // TODO: Measure avg message length
            Exception? closeException = null;

            while (true)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                try
                {
                    ValueWebSocketReceiveResult result = await ws.ReceiveAsync(memory, cancellationToken).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogDebug("Close received.");
                        break;
                    }

                    writer.Advance(result.Count);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    closeException = ex;
                    _logger.LogError(ex, "Exception receiving result.");
                    break;
                }

                // Make data available to the reader
                var flushResult = await writer.FlushAsync().ConfigureAwait(false);

                if (flushResult.IsCompleted) break;
            }

            await writer.CompleteAsync().ConfigureAwait(false);

            _logger.LogDebug("Fill task completed.");

            if (_state != State.CloseRequested)
                await UnexpectedCloseAsync(closeException).ConfigureAwait(false);
        }

        private async Task ReadPipeAsync(PipeReader reader)
        {
            try
            {
                while (true)
                {
                    ReadResult readResult = await reader.ReadAsync().ConfigureAwait(false);

                    if (readResult.IsCanceled) break;

                    ReadOnlySequence<byte> buffer = readResult.Buffer;

                    try
                    {
                        while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                        {
                            var evt = Received;
                            if (evt is not null)
                            {
                                try
                                {
                                    await evt.Invoke(line).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, $"Exception executing {nameof(Received)} handler.");
                                }
                            }
                        }
                    }
                    finally
                    {
                        reader.AdvanceTo(buffer.Start, buffer.End);
                    }

                    if (readResult.IsCompleted) break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in reader task.");
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);

                _logger.LogDebug("Read task completed.");
            }
        }

        private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            // Every message ends with "\r\n"
            SequencePosition? currentEndPos = buffer.PositionOf((byte)'\r');

            if (!currentEndPos.HasValue)
            {
                line = default;
                return false;
            }

            line = buffer.Slice(0, currentEndPos.Value);

            // Skip the line + "\r\n"
            SequencePosition nextStartPos = buffer.GetPosition(2, currentEndPos.Value);
            buffer = buffer.Slice(nextStartPos);

            return true;
        }

        public void Dispose()
        {
            _ws?.Dispose();
            _stoppingToken?.Dispose();
            _connectSem?.Dispose();
        }
    }
}
