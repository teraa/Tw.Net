using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Twitch.PubSub
{
    public class Topic
    {
        public string Name { get; init; } = null!;
        public IReadOnlyList<string> Args { get; init; } = null!;

        public static Topic Parse(string input)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));

            var parts = input.Split('.');
            if (parts.Length < 2)
                throw new FormatException("Missing topic arguments");

            string name = parts[0];
            string[] args = parts[1..];

            return new Topic
            {
                Name = name,
                Args = args
            };
        }

        public override string ToString()
            => $"{Name}.{string.Join('.', Args)}";
    }

    internal class TopicConverter : JsonConverter<Topic>
    {
        public override Topic? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetString() is string str ? Topic.Parse(str) : null;
        }

        public override void Write(Utf8JsonWriter writer, Topic value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
