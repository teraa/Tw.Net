using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public sealed class TextTcpClient : ISocketClient, IDisposable
    {
        private readonly string _hostname;
        private readonly ushort _port;
        private readonly bool _ssl;
        private readonly Encoding _encoding;
        private readonly SemaphoreSlim _connectSem;
        private TcpClient? _client;
        private Stream? _stream;
        private StreamReader? _sr;
        private StreamWriter? _sw;
        private bool _disposedValue;

        public TextTcpClient(string hostname, ushort port, bool ssl, Encoding encoding)
        {
            _hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            _port = port;
            _ssl = ssl;
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _connectSem = new SemaphoreSlim(1, 1);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_hostname, _port, cancellationToken).ConfigureAwait(false);

                _stream = _client.GetStream();
                if (_ssl)
                {
                    var stream = new SslStream(_stream, false);
                    var sslOptions = new SslClientAuthenticationOptions
                    {
                        TargetHost = _hostname
                    };
                    await stream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                    _stream = stream;
                }

                _sr = new StreamReader(_stream, _encoding);
                _sw = new StreamWriter(_stream, _encoding);
            }
            finally
            {
                _connectSem.Release();
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            await _connectSem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _client?.Dispose();
                _client = null;
                _stream?.Dispose();
                _stream = null;
                _sr = null;
                _sw = null;
            }
            catch { }
            finally
            {
                _connectSem.Release();
            }
        }

        public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_sr is null)
                throw new InvalidOperationException("Cannot read while disconnected.");

            var cancelTask = Task.Delay(-1, cancellationToken);
            var readTask = _sr.ReadLineAsync();
            var resultTask = await Task.WhenAny(readTask, cancelTask).ConfigureAwait(false);

            if (resultTask == cancelTask)
                throw new TaskCanceledException();

            return await readTask.ConfigureAwait(false);
        }

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));
            if (_sw is null)
                throw new InvalidOperationException("Cannot send while disconnected.");

            await _sw.WriteLineAsync(message.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _sw.FlushAsync().ConfigureAwait(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try { _client?.Dispose(); } catch { }
                    try { _stream?.Dispose(); } catch { }
                    try { _sr?.Dispose(); } catch { }
                    try { _sw?.Dispose(); } catch { }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
