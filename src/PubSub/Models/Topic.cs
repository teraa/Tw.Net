using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Twitch.PubSub
{
    public class Topic
    {
        public string Name { get; }
        public IReadOnlyList<string> Args { get; }

        public Topic(string name, IReadOnlyList<string> args)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Args = args ?? throw new ArgumentNullException(nameof(args));
        }

        public static Topic Parse(string input)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));

            var parts = input.Split('.');
            if (parts.Length < 2)
                throw new FormatException("Missing topic arguments");

            return new Topic(name: parts[0], args: parts[1..]);
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
