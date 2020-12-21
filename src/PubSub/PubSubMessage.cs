using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Twitch.PubSub
{
    public class PubSubMessage
    {
        [JsonPropertyName("type")]
        public MessageType Type { get; init; }

        [JsonPropertyName("data")]
        public MessageData? Data { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("nonce")]
        public string? Nonce { get; init; }

        public enum MessageType : byte
        {
            PING,
            PONG,
            LISTEN,
            UNLISTEN,
            RESPONSE,
            MESSAGE
        }
        public class MessageData
        {
            [JsonPropertyName("topic")]
            public string? Topic { get; init; }

            [JsonPropertyName("message")]
            public string? Message { get; init; }

            [JsonPropertyName("topics")]
            public IReadOnlyList<string>? Topics { get; init; }

            [JsonPropertyName("auth_token")]
            public string? AuthToken { get; init; }
        }
    }
}