using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Twitch.Clients
{
    public class TcpSocketClient : ISocketClient
    {
        private readonly ILogger<TcpSocketClient> _logger;
        private readonly TcpClient _tcpClient;
        private readonly SemaphoreSlim _connectSem;
        private Stream? _stream;
        private PipeReader? _pipeReader;
        private Task? _readTask;
        private State _state;
        private string _host;

        public TcpSocketClient(string host, int port, bool ssl, ILogger<TcpSocketClient> logger)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
            UseSsl = ssl;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tcpClient = new TcpClient();
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

        public string Host
        {
            get => _host;
            set => _host = value ?? throw new ArgumentNullException(nameof(Host));
        }

        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public byte[] MessageDelimiter { get; set; } = new byte[] { (byte)'\r', (byte)'\n' };

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state == State.Open)
                    throw new InvalidOperationException("Client was already connected.");

                await _tcpClient.ConnectAsync(Host, Port, cancellationToken).ConfigureAwait(false);

                _stream = _tcpClient.GetStream();

                if (UseSsl)
                {
                    var sslStream = new SslStream(_stream, false);
                    var sslOptions = new SslClientAuthenticationOptions
                    {
                        TargetHost = Host,
                    };

                    await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                    _stream = sslStream;
                }

                _state = State.Open;

                _pipeReader = PipeReader.Create(_stream);
                _readTask = ReadPipeAsync(_pipeReader);
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

                try
                {
                    _tcpClient.Close();
                }
                catch { }

                _pipeReader!.CancelPendingRead();
                await _readTask!.ConfigureAwait(false);

                _readTask = null;
                _pipeReader = null;

                _stream!.Dispose();
                _stream = null;

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

                _tcpClient.Close();

                // Cannot await, would deadlock
                _readTask = null;
                _pipeReader = null;

                _stream!.Dispose();
                _stream = null;

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
            Stream stream;

            // Ensure we don't get a nullref
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state != State.Open)
                    throw new InvalidOperationException($"Cannot send while not open. Current state: {_state}");

                stream = _stream!;
            }
            finally
            {
                _connectSem.Release();
            }

            // TODO: Flush?
            await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        private async Task ReadPipeAsync(PipeReader reader)
        {
            Exception? closeException = null;
            try
            {
                while (true)
                {
                    ReadResult readResult = await reader.ReadAsync().ConfigureAwait(false);

                    if (readResult.IsCanceled) break;

                    ReadOnlySequence<byte> buffer = readResult.Buffer;

                    try
                    {
                        while (ReadOnlySequenceExtensions.TryReadMessage(ref buffer, out ReadOnlySequence<byte> message, MessageDelimiter))
                        {
                            var evt = Received;
                            if (evt is not null)
                            {
                                try
                                {
                                    await evt.Invoke(message).ConfigureAwait(false);
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
                closeException = ex;
                _logger.LogError(ex, "Exception in reader task.");
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);

                _logger.LogDebug("Read task completed.");
            }

            if (_state != State.CloseRequested)
                await UnexpectedCloseAsync(closeException).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _tcpClient.Dispose();
            _connectSem.Dispose();
            _stream?.Dispose();
        }
    }
}
