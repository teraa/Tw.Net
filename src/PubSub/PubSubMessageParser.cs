using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Twitch.PubSub
{
    internal static class PubSubMessageParser
    {
        private static readonly JsonSerializerOptions s_options;

        static PubSubMessageParser()
        {
            s_options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public static PubSubMessage Parse(string input)
        {
            return JsonSerializer.Deserialize<PubSubMessage>(input, s_options)
                ?? throw new FormatException("Message cannot be null");
        }

        // TODO: ?
        public static string ToJson<T>(T value)
        {
            return JsonSerializer.Serialize<T>(value, s_options);
        }
    }
}
