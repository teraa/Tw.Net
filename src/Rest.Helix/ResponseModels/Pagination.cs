using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Twitch.Rest.Helix
{
    #nullable disable
    [DebuggerDisplay("{Cursor,nq}")]
    public class Pagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; init; }
    }
    #nullable restore
}
