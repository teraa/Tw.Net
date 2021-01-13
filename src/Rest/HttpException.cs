using System;
using System.Net;

namespace Twitch.Rest
{
    public class HttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string Reason { get; }

        public HttpException(HttpStatusCode statusCode, string reason)
            : base($"{(int)statusCode}: {reason}")
        {
            StatusCode = statusCode;
            Reason = reason;
        }

        public HttpException(HttpStatusCode statusCode)
            : this(statusCode, statusCode.ToString()) { }
    }
}
