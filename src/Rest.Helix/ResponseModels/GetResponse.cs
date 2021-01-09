using System.Text.Json.Serialization;

namespace Twitch.Rest.Helix
{
#nullable disable
    public class GetResponse<T>
    {
        [JsonPropertyName("data")]
        public T[] Data { get; init; }

        [JsonPropertyName("pagination")]
        public Pagination Pagination { get; init; }
    }
#nullable restore
}
