using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Twitch.PubSub.Messages;

namespace Twitch.PubSub
{
    internal static class PubSubParser
    {
        private static readonly JsonSerializerOptions s_options;

        static PubSubParser()
        {
            s_options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new TopicConverter()
                }
            };
        }

        public static T Parse<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, s_options)
                ?? throw new FormatException();
        }

        public static string ToJson<T>(T value)
        {
            return JsonSerializer.Serialize<T>(value, s_options);
        }

        public static object? ParseMessage(Topic topic, string messageJson)
        {
            if (topic is null) throw new ArgumentNullException(nameof(topic));
            if (messageJson is null) throw new ArgumentNullException(nameof(messageJson));

            switch (topic.Name)
            {
                case "chat_moderator_actions":
                    {
                        var message = Parse<ChatModeratorActionsMessage>(messageJson);
                        return ModeratorAction.Create(topic, message);
                    }
            }

            return null;
        }
    }
}
