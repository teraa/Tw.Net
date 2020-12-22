using System;
using System.Collections.Generic;
using Twitch.PubSub.Messages;

namespace Twitch.PubSub
{
    public class ModeratorAction
    {
        public string ChannelId { get; init; } = null!;
        public User? Moderator { get; init; } = null!;
        public User? Target { get; init; }
        public string Type { get; init; } = null!;
        public string Action { get; init; } = null!;
        public IReadOnlyList<string>? Args { get; init; }
        public string? MessageId { get; init; }
        public bool IsFromAutomod { get; init; }
        public string? ModeratorMessage { get; init; }

        internal static ModeratorAction Create(Topic topic, ChatModeratorActionsMessage message)
        {
            var channelId = topic.Args[1];
            var data = message.Data ?? throw new ArgumentNullException(nameof(message.Data));

            User? moderator = null;
            var moderatorId = data.CreatedByUserId ??  data.CreatedById;
            if (moderatorId is { Length: > 0})
            {
                moderator = new User
                {
                    Id = moderatorId,
                    Login = data.CreatedBy ?? data.CreatedByLogin
                        ?? throw new ArgumentNullException(nameof(data.CreatedBy))
                };
            }

            User? target = null;
            if (data.TargetUserId is { Length: > 0 })
            {
                string targetLogin;
                if (data.TargetUserLogin is { Length: > 0 })
                    targetLogin = data.TargetUserLogin;
                else
                    targetLogin = data.Args![0]
                        ?? throw new ArgumentNullException(data.TargetUserLogin);

                target = new User
                {
                    Id = data.TargetUserId,
                    Login = targetLogin
                };
            }

            var type = data.Type ?? message.Type
                ?? throw new ArgumentNullException(nameof(data.Type));

            var action = data.ModerationAction
                ?? throw new ArgumentNullException(nameof(data.ModerationAction));

            var args = data.Args;

            string? messageId;
            if (string.Equals(action, "delete", StringComparison.Ordinal))
                messageId = args![2];
            else if (data.MsgId is { Length: > 0})
                messageId = data.MsgId;
            else
                messageId = null;

            var isFromAutomod = data.FromAutomod == true || moderator is null;

            var moderatorMessage = data.ModeratorMessage;

            return new ModeratorAction
            {
                ChannelId = channelId,
                Moderator = moderator,
                Target = target,
                Type = type,
                Action = action,
                Args = args,
                MessageId = messageId,
                IsFromAutomod = isFromAutomod,
                ModeratorMessage = moderatorMessage
            };
        }
    }
}
