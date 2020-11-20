using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public sealed class TextWebSocketClient : ISocketClient
    {
        private readonly Uri _uri;
        private readonly Encoding _encoding;
        private readonly Decoder _decoder;
        private readonly byte[] _sbuf;
        private readonly byte[] _rbuf;
        private readonly char[] _rcbuf;
        private int _rcp;
        private int _rcl;
        private ClientWebSocket? _client;

        public TextWebSocketClient(Uri uri, Encoding encoding)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _decoder = _encoding.GetDecoder();
            _sbuf = new byte[4096];
            _rbuf = new byte[4096];
            _rcbuf = new char[_encoding.GetMaxCharCount(_rbuf.Length)];
            _rcp = 0;
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
                var client = _client;
                _client = null;

                if (client is not null)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, default, default);
                    client.Dispose();
                }
            }
            catch { }
        }

        public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_client is null)
                throw new InvalidOperationException("Cannot read while disconnected.");
            if (_client.State != WebSocketState.Open)
                throw new InvalidOperationException($"Cannot read while socket is not open. Current state: {_client.State}");

            int read;

            if (_rcp == _rcl)
            {
                read = await ReadBufferAsync(cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }
            }

            StringBuilder? sb = null;
            var i = _rcp;
            do
            {
                do
                {
                    char c = _rcbuf[i];

                    if (c is '\r' or '\n')
                    {
                        string line;

                        if (sb is null)
                        {
                            line = new string(_rcbuf, _rcp, i - _rcp);
                        }
                        else
                        {
                            sb.Append(_rcbuf, _rcp, i - _rcp);
                            line = sb.ToString();
                        }

                        _rcp = i + 1;

                        if (c == '\r')
                        {
                            if (_rcp < _rcl && _rcbuf[_rcp] == '\n')
                            {
                                _rcp++;
                            }
                            else
                            {
                                read = await ReadBufferAsync(cancellationToken).ConfigureAwait(false);
                                if (read > 0 && _rcbuf[_rcp] == '\n')
                                {
                                    _rcp++;
                                }
                            }
                        }

                        return line;
                    }

                    i++;
                } while (i < _rcl);

                i = _rcl - _rcp;
                sb ??= new StringBuilder(i + 80);
                sb.Append(_rcbuf, _rcp, i);

                read = await ReadBufferAsync(cancellationToken).ConfigureAwait(false);
            } while (read > 0);

            return sb.ToString();
        }

        private async Task<int> ReadBufferAsync(CancellationToken cancellationToken)
        {
            _rcl = 0;
            _rcp = 0;

            do
            {
                var result = await _client.ReceiveAsync(_rbuf, cancellationToken);

                if (result.Count == 0)
                {
                    return _rcl;
                }

                _rcl += _decoder.GetChars(_rbuf, 0, result.Count, _rcbuf, 0);
            } while (_rcl == 0);

            return _rcl;
        }

        public async Task SendAsync(string message)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));
            if (_client is null)
                throw new InvalidOperationException("Cannot send while disconnected.");

            int count = _encoding.GetByteCount(message);
            if (count <= _sbuf.Length)
            {
                var segment = new ArraySegment<byte>(_sbuf, 0, count);
                _encoding.GetBytes(message, segment);
                await _client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                var arr = message.ToCharArray(); 
                int p = 0;
                do
                {
                    int len = count - p;
                    if (len > _sbuf.Length)
                        len = _sbuf.Length;

                    var charSegment = new ArraySegment<char>(arr, p, len);
                    var byteSegment = new ArraySegment<byte>(_sbuf, 0, len);
                    _encoding.GetBytes(charSegment, byteSegment);

                    p += len;
                    bool endOfMessage = p == count;

                    await _client.SendAsync(byteSegment, WebSocketMessageType.Text, endOfMessage, CancellationToken.None).ConfigureAwait(false);
                } while (p < count);
            }
        }
    }
}
