using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public sealed class TextWebSocketClient : ISocketClient, IDisposable
    {
        private readonly Uri _uri;
        private readonly Encoding _encoding;
        private readonly Memory<byte> _sbuf;
        private readonly Memory<byte> _rbuf;
        private ClientWebSocket? _client;
        private MemoryStream? _ms;
        private StreamReader? _sr;
        private bool _disposedValue;

        public TextWebSocketClient(Uri uri, Encoding encoding)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _sbuf = new Memory<byte>(new byte[4096]);
            _rbuf = new Memory<byte>(new byte[4096]);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _client = new ClientWebSocket();
            await _client.ConnectAsync(_uri, cancellationToken).ConfigureAwait(false);
        }

        public async void Disconnect()
        {
            try
            {
                if (_client is ClientWebSocket client)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, default, default).ConfigureAwait(false);
                    client.Dispose();
                }

                _client = null;

                _sr?.Dispose();
                _sr = null;

                _ms?.Dispose();
                _ms = null;
            }
            catch { }
        }

        public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_sr is null)
            {
                if (_client is null)
                    throw new InvalidOperationException("Cannot read while disconnected.");
                if (_client.State != WebSocketState.Open)
                    throw new InvalidOperationException($"Cannot read while socket is not open. Current state: {_client.State}.");

                _ms = new MemoryStream();
                ValueWebSocketReceiveResult wsresult;
                do
                {
                    wsresult = await _client.ReceiveAsync(_rbuf, cancellationToken).ConfigureAwait(false);
                    await _ms.WriteAsync(_rbuf[..wsresult.Count], cancellationToken).ConfigureAwait(false);
                } while (!wsresult.EndOfMessage);

                _ms.Seek(0, SeekOrigin.Begin);

                _sr = new StreamReader(_ms, _encoding);
            }

            var result = await _sr.ReadLineAsync().ConfigureAwait(false);

            if (_sr.EndOfStream)
            {
                _sr.Dispose();
                _sr = null;

                _ms!.Dispose();
                _ms = null;
            }

            return result;
        }

        public async Task SendAsync(string message)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));
            if (_client is null)
                throw new InvalidOperationException("Cannot send while disconnected.");
            if (_client.State != WebSocketState.Open)
                throw new InvalidOperationException($"Cannot send while socket is not open. Current state: {_client.State}.");

            Memory<byte> bytes;
            int count = _encoding.GetByteCount(message);
            if (count <= _sbuf.Length)
            {
                bytes = _sbuf[..count];
                _encoding.GetBytes(message, bytes.Span);
            }
            else
            {
                bytes = _encoding.GetBytes(message).AsMemory();
            }

            await _client.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try { _client?.Dispose(); } catch { }
                    try { _ms?.Dispose(); } catch { }
                    try { _sr?.Dispose(); } catch { }
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
