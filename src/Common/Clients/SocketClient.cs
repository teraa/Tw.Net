using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Twitch.Clients
{
    // TODO: unfinished
    public class SocketClient : ISocketClient
    {
        private readonly ILogger<SocketClient> _logger;
        private readonly Socket _socket;
        private Stream? _stream;
        private PipeReader? _pipeReader;
        private Task? _readTask;

        public SocketClient(EndPoint endpoint, ProtocolType protocol, bool ssl, ILogger<SocketClient> logger)
        {
            Endpoint = endpoint;
            Protocol = protocol;
            UseSsl = ssl;
            _logger = logger;
            _socket = new Socket(SocketType.Stream, protocol);
        }

        public event Func<ReadOnlySequence<byte>, ValueTask>? Received;
        public event Func<Exception?, ValueTask>? ClosedUnexpectedly;

        public EndPoint Endpoint { get; set; }
        public ProtocolType Protocol { get; set; }
        public bool UseSsl { get; set; }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _socket.ConnectAsync(Endpoint, cancellationToken).ConfigureAwait(false);

            _stream = new NetworkStream(_socket);

            if (UseSsl)
            {
                var sslStream = new SslStream(_stream, false);
                string targetHost = Endpoint switch
                {
                    IPEndPoint ip => ip.Address.ToString(),
                    DnsEndPoint dns => dns.Host,
                    _ => throw new ArgumentException($"Unknown EndPoint type: {(Endpoint.GetType())}")
                };

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost,
                };

                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                _stream = sslStream;
            }

            _readTask = ReadStreamAsync(_stream);
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _socket.Close();
            }
            catch { }

            _pipeReader?.CancelPendingRead();

            if (_readTask is Task readTask)
                await readTask.ConfigureAwait(false);

            _readTask = null;
            _pipeReader = null;

            _stream?.Dispose();
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // TODO: Flush?
            await _stream!.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        private async Task ReadStreamAsync(Stream stream)
        {
            // TODO: Handle exceptions

            var reader = PipeReader.Create(stream);
            _pipeReader = reader;

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
    }
}
