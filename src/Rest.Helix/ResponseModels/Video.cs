using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Twitch.Rest.Helix
{
    #nullable disable
    [DebuggerDisplay("Id = {Id,nq}")]
    public class Video
    {
        /// <summary>
        ///     ID of the video.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; init; }

        /// <summary>
        ///     ID of the user who owns the video.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string UserId { get; init; }

        /// <summary>
        ///     Display name of the user who owns the video.
        /// </summary>
        [JsonPropertyName("user_name")]
        public string UserName { get; init; }

        /// <summary>
        ///     Title of the video.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; init; }

        /// <summary>
        ///     Description of the video.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; init; }

        /// <summary>
        ///     Date and time when the video was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        ///     Date and time when the video was published.
        /// </summary>
        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; init; }

        /// <summary>
        ///     URL of the video.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; init; }

        /// <summary>
        ///     Template URL for the thumbnail of the video.
        /// </summary>
        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; init; }

        /// <summary>
        ///     Indicates whether the video is publicly viewable.
        ///     Valid values are: <c>"public"</c> and <c>"private"</c>.
        /// </summary>
        [JsonPropertyName("viewable")]
        public string Viewable { get; init; }

        /// <summary>
        ///     Number of times the video has been viewed.
        /// </summary>
        [JsonPropertyName("view_count")]
        public long ViewCount { get; init; }

        /// <summary>
        ///     Language of the video.
        /// </summary>
        [JsonPropertyName("language")]
        public string Language { get; init; }

        /// <summary>
        ///     Type of the video. Valid values are:
        ///     <c>"upload"</c>, <c>"archive"</c> and <c>"highlight"</c>.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; init; }

        /// <summary>
        ///     Length of the video, e.g. <c>3h8m33s</c>
        /// </summary>
        [JsonPropertyName("duration")]
        public string Duration { get; init; }
    }
    #nullable restore
}
