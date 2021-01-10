namespace Twitch.Rest.Helix
{
    public class GetFollowsArgs : IRequestArgs
    {
        [QueryParam("to_id")]
        public string? ToId { get; init; }

        [QueryParam("from_id")]
        public string? FromId { get; init; }

        [QueryParam("first")]
        public int? First { get; init; }

        [QueryParam("after")]
        public string? After { get; init; }
    }
}
