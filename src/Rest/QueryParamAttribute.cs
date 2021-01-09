using System;

namespace Twitch.Rest
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class QueryParamAttribute : Attribute
    {
        public QueryParamAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        public string Name { get; }
    }
}
