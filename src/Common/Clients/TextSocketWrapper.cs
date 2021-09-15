using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Twitch.Clients
{
    // TODO: remove
    public class TextSocketWrapper
    {
        private readonly ISocketClient _client;
        private readonly ILogger<TextSocketWrapper> _logger;

        public TextSocketWrapper(ISocketClient client, ILogger<TextSocketWrapper> logger)
        {
            _client = client;
            _logger = logger;

            _client.Received += ReceivedAsync;
        }

        public event Func<string, ValueTask>? Received;

        public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public async ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
        {
            // ClientWebSocket doesnt actually need \r\n for sending
            byte[] bytes = Encoding.GetBytes(message + "\r\n");
            await _client.SendAsync(bytes, cancellationToken);
        }

        private async ValueTask ReceivedAsync(ReadOnlySequence<byte> buffer)
        {
            var text = Encoding.GetString(buffer);

            var evt = Received;
            if (evt is not null)
            {
                try
                {
                    await evt.Invoke(text).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Exception executing {nameof(Received)} handler.");
                }
            }
        }
    }
}
