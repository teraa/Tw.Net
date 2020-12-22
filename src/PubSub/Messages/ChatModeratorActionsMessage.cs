using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Twitch.PubSub.Messages
{
    internal class ChatModeratorActionsMessage
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("data")]
        public MessageData? Data { get; init; }

        public class MessageData
        {
            [JsonPropertyName("type")]
            public string? Type { get; init; }

            [JsonPropertyName("moderation_action")]
            public string? ModerationAction { get; init; }

            [JsonPropertyName("args")]
            public IReadOnlyList<string>? Args { get; init; }

            [JsonPropertyName("channel_id")]
            public string? ChannelId { get; init; }

            [JsonPropertyName("created_by")]
            public string? CreatedBy { get; init; }

            [JsonPropertyName("created_by_login")]
            public string? CreatedByLogin { get; init; }

            [JsonPropertyName("created_by_id")]
            public string? CreatedById { get; init; }

            [JsonPropertyName("created_by_user_id")]
            public string? CreatedByUserId { get; init; }

            [JsonPropertyName("msg_id")]
            public string? MsgId { get; init; }

            [JsonPropertyName("target_user_id")]
            public string? TargetUserId { get; init; }

            [JsonPropertyName("target_user_login")]
            public string? TargetUserLogin { get; init; }

            [JsonPropertyName("from_automod")]
            public bool? FromAutomod { get; init; }

            [JsonPropertyName("moderator_message")]
            public string? ModeratorMessage { get; init; }
        }
    }
}
