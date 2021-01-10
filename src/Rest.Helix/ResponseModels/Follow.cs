using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Twitch.Rest.Helix
{
#nullable disable
    public class Follow
    {
        [JsonPropertyName("from_id")]
        public string FromId { get; init; }

        [JsonPropertyName("from_name")]
        public string FromName { get; init; }

        [JsonPropertyName("to_id")]
        public string ToId { get; init; }

        [JsonPropertyName("to_name")]
        public string ToName { get; init; }

        [JsonPropertyName("followed_at")]
        public string FollowedAt { get; init; }
    }
#nullable restore
}
