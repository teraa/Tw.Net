using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Twitch.Rest.Helix
{
#nullable disable
    [DebuggerDisplay("Id = {Id,nq}, Login = {Login,nq}")]
    public class User
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = null!;

        [JsonPropertyName("login")]
        public string Login { get; init; } = null!;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; init; } = null!;

        [JsonPropertyName("type")]
        public string Type { get; init; } = null!;

        [JsonPropertyName("broadcaster_type")]
        public string BroadcasterType { get; init; } = null!;

        [JsonPropertyName("description")]
        public string Description { get; init; } = null!;

        [JsonPropertyName("profile_image_url")]
        public string ProfileImageUrl { get; init; } = null!;

        [JsonPropertyName("offline_image_url")]
        public string OfflineImageUrl { get; init; } = null!;

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }
    }
#nullable restore
}
