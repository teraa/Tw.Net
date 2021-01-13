using System.Text.Json.Serialization;

namespace Twitch.Rest.Helix
{
#nullable disable
    internal class ErrorResponse
    {

        [JsonPropertyName("error")]
        public string Error { get; init; }

        [JsonPropertyName("status")]
        public int Status { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; }
    }
#nullable restore
}
