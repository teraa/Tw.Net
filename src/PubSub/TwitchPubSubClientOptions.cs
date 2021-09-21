using System;
using System.Text;

namespace Twitch.PubSub
{
    public interface TwitchPubSubClientOptions
    {
        Encoding Encoding { get; set; }
        TimeSpan PingInterval { get; set; }
        TimeSpan PongTimeout { get; set; }
        TimeSpan ResponseTimeout { get; set; }
    }
}
