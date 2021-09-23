using System;
using System.Collections.Generic;
using Twitch.PubSub.Messages;

namespace Twitch.PubSub
{
    public class ModeratorAction
    {
        public string ChannelId { get; private init; } = null!;
        public User? Moderator { get; private init; } = null!;
        public User? Target { get; private init; }
        public string Type { get; private init; } = null!;
        public string Action { get; private init; } = null!;
        public IReadOnlyList<string>? Args { get; private init; }
        public string? MessageId { get; private init; }
        public bool IsFromAutomod { get; private init; }
        public string? ModeratorMessage { get; private init; }

        // TODO: help me
        internal static ModeratorAction Create(Topic topic, ChatModeratorActionsMessage message)
        {
            var channelId = topic.Args[1];
            var data = message.Data ?? throw new ArgumentNullException(nameof(message.Data));

            User? moderator = null;
            var moderatorId = data.CreatedByUserId ?? data.CreatedById ?? data.RequesterId;
            if (moderatorId is { Length: > 0 })
            {
                moderator = new User
                {
                    Id = moderatorId,
                    Login = data.CreatedBy ?? data.CreatedByLogin ?? data.RequesterLogin
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

            string type, action;

            if (data.ModerationAction is null)
            {
                type = message.Type ?? throw new ArgumentNullException(nameof(message.Type));
                action = data.Type ?? message.Type ?? throw new ArgumentNullException(nameof(data.ModerationAction));
            }
            else
            {
                type = data.Type ?? message.Type ?? throw new ArgumentNullException(nameof(data.Type));
                action = data.ModerationAction;
            }

            IReadOnlyList<string>? args;
            if (string.Equals(message.Type, "channel_terms_action", StringComparison.Ordinal) && data.Text is not null)
                args = new string[] { data.Text };
            else
                args = data.Args;

            string? messageId;
            if (string.Equals(action, "delete", StringComparison.Ordinal))
                messageId = args![2];
            else if (data.MsgId is { Length: > 0 })
                messageId = data.MsgId;
            else
                messageId = null;

            var isFromAutomod = data.FromAutomod == true;

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
