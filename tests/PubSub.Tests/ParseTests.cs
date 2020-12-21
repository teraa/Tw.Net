using System;
using Xunit;

namespace Twitch.PubSub.Tests
{
    public class ParseTests
    {
        [Fact]
        public void JsonToMessage()
        {
            var message = PubSubMessageParser.Parse("{}");
            Assert.Null(message.Data);
            Assert.Null(message.Error);
            Assert.Null(message.Nonce);
            Assert.Equal(PubSubMessage.MessageType.Unknown, message.Type);
        }

        [Fact]
        public void MessageToJson()
        {
            var message = new PubSubMessage
            {
                Type = PubSubMessage.MessageType.PING
            };
            var json = PubSubMessageParser.ToJson(message);

            Assert.Equal(@"{""type"":""PING""}", json);
        }

        [Fact]
        public void TopicParse1()
        {
            var raw = "name.123";
            var topic = Topic.Parse(raw);
            Assert.Equal("name", topic.Name);
            Assert.Equal(1, topic.Args.Count);
            Assert.Equal("123", topic.Args[0]);
        }

        [Fact]
        public void TopicParse2()
        {
            var raw = "name.123.456";
            var topic = Topic.Parse(raw);
            Assert.Equal("name", topic.Name);
            Assert.Equal(2, topic.Args.Count);
            Assert.Equal("123", topic.Args[0]);
            Assert.Equal("456", topic.Args[1]);
        }

        [Fact]
        public void TopicParse_Invalid()
        {
            Assert.Throws<FormatException>(() => Topic.Parse("invalid"));
        }
    }
}
