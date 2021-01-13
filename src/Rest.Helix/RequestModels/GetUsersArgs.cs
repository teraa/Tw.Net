using System.Collections.Generic;

namespace Twitch.Rest.Helix
{
    public class GetUsersArgs : IRequestArgs
    {
        [QueryParam("login")]
        public IReadOnlyList<string>? Logins { get; init; }

        [QueryParam("id")]
        public IReadOnlyList<string>? Ids { get; init; }
    }
}
