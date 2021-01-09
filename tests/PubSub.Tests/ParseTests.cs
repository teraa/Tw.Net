using System;
using Twitch.PubSub.Messages;
using Xunit;

namespace Twitch.PubSub.Tests
{
    public class ParseTests
    {
        [Fact]
        public void JsonToMessage()
        {
            var message = PubSubParser.Parse<PubSubMessage>("{}");
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
            var json = PubSubParser.ToJson(message);

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

        public static object ParseModel(string json)
        {
            var msg = PubSubParser.Parse<PubSubMessage>(json);
            Assert.NotNull(msg);
            Assert.NotNull(msg.Data);
            Assert.NotNull(msg.Data.Message);
            Assert.NotNull(msg.Data.Topic);
            return PubSubParser.ParseMessage(msg.Data.Topic, msg.Data.Message);
        }

        public static ModeratorAction ParseModeratorAction(string json)
        {
            var model = ParseModel(json);
            Assert.NotNull(model);
            Assert.IsType<ModeratorAction>(model);
            return (ModeratorAction)model;
        }

        [Fact]
        public void ParseModeratorAction_Delete()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""delete\"",\""args\"":[\""<TARGET_LOGIN>\"",\""message content\"",\""<MESSAGE_ID>\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("delete", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(3, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("message content", act.Args[1]);
            Assert.Equal("<MESSAGE_ID>", act.Args[2]);
            Assert.Equal("<MESSAGE_ID>", act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Ban_Reason()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""ban\"",\""args\"":[\""<TARGET_LOGIN>\"",\""reason 1 2 3\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("ban", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(2, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("reason 1 2 3", act.Args[1]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Ban_NoReason()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""ban\"",\""args\"":[\""<TARGET_LOGIN>\"",\""\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("ban", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(2, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("", act.Args[1]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Unban()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""unban\"",\""args\"":[\""<TARGET_LOGIN>\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("unban", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Timeout_Reason()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""timeout\"",\""args\"":[\""<TARGET_LOGIN>\"",\""23\"",\""reason 1 2 3\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("timeout", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(3, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("23", act.Args[1]);
            Assert.Equal("reason 1 2 3", act.Args[2]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Timeout_NoReason()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""timeout\"",\""args\"":[\""<TARGET_LOGIN>\"",\""600\"",\""\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("timeout", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(3, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("600", act.Args[1]);
            Assert.Equal("", act.Args[2]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Untimeout()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""untimeout\"",\""args\"":[\""<TARGET_LOGIN>\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("untimeout", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Clear()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""clear\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("clear", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Emoteonly()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""emoteonly\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("emoteonly", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Emoteonlyoff()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""emoteonlyoff\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("emoteonlyoff", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Followers1()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""followers\"",\""args\"":[\""1440\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("followers", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("1440", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Followers2()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""followers\"",\""args\"":[\""0\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("followers", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("0", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Followersoff()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""followersoff\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("followersoff", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Slow()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""slow\"",\""args\"":[\""12\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("slow", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("12", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Slowoff()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""slowoff\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("slowoff", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Subscribers()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""subscribers\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("subscribers", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Subscribersoff()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""subscribersoff\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("subscribersoff", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_R9kbeta()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""r9kbeta\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("r9kbeta", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_R9kbetaoff()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""r9kbetaoff\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("r9kbetaoff", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Host()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""host\"",\""args\"":[\""<TARGET_LOGIN>\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("host", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Unhost()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""unhost\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("unhost", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Raid()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""raid\"",\""args\"":[\""<TARGET_LOGIN>\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("raid", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Unraid()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""unraid\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("unraid", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Mod()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderator_added\"",\""data\"":{\""channel_id\"":\""<CHANNEL_ID>\"",\""target_user_id\"":\""<TARGET_ID>\"",\""moderation_action\"":\""mod\"",\""target_user_login\"":\""<TARGET_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""created_by\"":\""<MOD_LOGIN>\""}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("moderator_added", act.Type);
            Assert.Equal("mod", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_Unmod()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderator_removed\"",\""data\"":{\""channel_id\"":\""<CHANNEL_ID>\"",\""target_user_id\"":\""<TARGET_ID>\"",\""moderation_action\"":\""unmod\"",\""target_user_login\"":\""<TARGET_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""created_by\"":\""<MOD_LOGIN>\""}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("moderator_removed", act.Type);
            Assert.Equal("unmod", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_ApproveUnbanRequest()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""approve_unban_request\"",\""data\"":{\""moderation_action\"":\""APPROVE_UNBAN_REQUEST\"",\""created_by_id\"":\""<MOD_ID>\"",\""created_by_login\"":\""<MOD_LOGIN>\"",\""moderator_message\"":\""Unban reply message\\nNew line\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""<TARGET_LOGIN>\""}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("approve_unban_request", act.Type);
            Assert.Equal("APPROVE_UNBAN_REQUEST", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Equal("Unban reply message\nNew line", act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_DenyUnbanRequest()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""deny_unban_request\"",\""data\"":{\""moderation_action\"":\""DENY_UNBAN_REQUEST\"",\""created_by_id\"":\""<MOD_ID>\"",\""created_by_login\"":\""<MOD_LOGIN>\"",\""moderator_message\"":\""Unban deny\\nNew line\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""<TARGET_LOGIN>\""}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("deny_unban_request", act.Type);
            Assert.Equal("DENY_UNBAN_REQUEST", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Equal("Unban deny\nNew line", act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_AutomodRejectedOld()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""automod_rejected\"",\""args\"":[\""<TARGET_LOGIN>\"",\""test phrase\"",\""identity\""],\""created_by\"":\""\"",\""created_by_user_id\"":\""\"",\""msg_id\"":\""<MESSAGE_ID>\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""<TARGET_LOGIN>\""}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Null(act.Moderator);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("automod_rejected", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(3, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("test phrase", act.Args[1]);
            Assert.Equal("identity", act.Args[2]);
            Assert.Equal("<MESSAGE_ID>", act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_AutomodRejected()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""automod_rejected\"",\""args\"":[\""<TARGET_LOGIN>\"",\""test phrase\"",\""identity\""],\""created_by\"":\""\"",\""created_by_user_id\"":\""\"",\""msg_id\"":\""<MESSAGE_ID>\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""<TARGET_LOGIN>\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Null(act.Moderator);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("automod_rejected", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(3, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("test phrase", act.Args[1]);
            Assert.Equal("identity", act.Args[2]);
            Assert.Equal("<MESSAGE_ID>", act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_AutomodApprovedOld()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""data\"":{\""type\"":\""chat_login_moderation\"",\""moderation_action\"":\""approved_automod_message\"",\""args\"":[\""<TARGET_LOGIN>\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""<MESSAGE_ID>\"",\""target_user_id\"":\""<TARGET_ID>\"",\""target_user_login\"":\""<TARGET_LOGIN>\""}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.NotNull(act.Moderator);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.NotNull(act.Target);
            Assert.Equal("<TARGET_ID>", act.Target.Id);
            Assert.Equal("<TARGET_LOGIN>", act.Target.Login);
            Assert.Equal("chat_login_moderation", act.Type);
            Assert.Equal("approved_automod_message", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("<TARGET_LOGIN>", act.Args[0]);
            Assert.Equal("<MESSAGE_ID>", act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_AddBlockedTerm()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""add_blocked_term\"",\""args\"":[\""test phrase\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("add_blocked_term", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("test phrase", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_DeleteBlockedTerm()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""delete_blocked_term\"",\""args\"":[\""test phrase\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("delete_blocked_term", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("test phrase", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_AddPermittedTerm()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""add_permitted_term\"",\""args\"":[\""test phrase\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("add_permitted_term", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("test phrase", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_DeletePermittedTerm()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""delete_permitted_term\"",\""args\"":[\""test phrase\""],\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("delete_permitted_term", act.Action);
            Assert.NotNull(act.Args);
            Assert.Equal(1, act.Args.Count);
            Assert.Equal("test phrase", act.Args[0]);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }

        [Fact]
        public void ParseModeratorAction_ModifiedAutomodProperties()
        {
            var json = @"{""type"":""MESSAGE"",""data"":{""topic"":""chat_moderator_actions.<MY_ID>.<CHANNEL_ID>"",""message"":""{\""type\"":\""moderation_action\"",\""data\"":{\""type\"":\""chat_channel_moderation\"",\""moderation_action\"":\""modified_automod_properties\"",\""args\"":null,\""created_by\"":\""<MOD_LOGIN>\"",\""created_by_user_id\"":\""<MOD_ID>\"",\""msg_id\"":\""\"",\""target_user_id\"":\""\"",\""target_user_login\"":\""\"",\""from_automod\"":false}}""}}";
            var act = ParseModeratorAction(json);
            Assert.Equal("<CHANNEL_ID>", act.ChannelId);
            Assert.Equal("<MOD_ID>", act.Moderator.Id);
            Assert.Equal("<MOD_LOGIN>", act.Moderator.Login);
            Assert.Null(act.Target);
            Assert.Equal("chat_channel_moderation", act.Type);
            Assert.Equal("modified_automod_properties", act.Action);
            Assert.Null(act.Args);
            Assert.Null(act.MessageId);
            Assert.False(act.IsFromAutomod);
            Assert.Null(act.ModeratorMessage);
        }
    }
}
