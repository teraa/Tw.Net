using System;
using System.Net;

namespace Twitch.Rest
{
    public class HttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? Reason { get; }

        public HttpException(HttpStatusCode statusCode, string? reason)
            : base(CreateMessage(statusCode, reason))
        {
            StatusCode = statusCode;
            Reason = reason;
        }

        public HttpException(HttpStatusCode statusCode)
            : this(statusCode, null) { }

        private static string CreateMessage(HttpStatusCode statusCode, string? reason)
        {
            return reason is { Length: > 0 }
                ? $"{(int)statusCode} {statusCode}: {reason}"
                : $"{(int)statusCode} {statusCode}";
        }
    }
}
