using System;
using System.Collections.Generic;
using System.Text;

namespace Twitch.Irc
{
    public interface TwitchIrcClientOptions
    {
        Encoding Encoding { get; set; }
        TimeSpan PingInterval { get; set; }
        TimeSpan PongTimeout { get; set; }
        TimeSpan LoginTimeout { get; set; }
        List<string> Capabilities { get; }
        IRateLimiter JoinLimiter { get; set; }
        IRateLimiter CommandLimiter { get; set; }
    }
}
