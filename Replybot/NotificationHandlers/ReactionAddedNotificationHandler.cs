﻿using MediatR;
using Replybot.BusinessLayer;
using Replybot.Models;
using Replybot.Notifications;
using Replybot.TextCommands;

namespace Replybot.NotificationHandlers;

public class ReactionAddedNotificationHandler : INotificationHandler<ReactionAddedNotification>
{
    private readonly IGuildConfigurationBusinessLayer _configurationBusinessLayer;
    private readonly KeywordHandler _keywordHandler;
    private readonly FixTwitterCommand _fixTwitterCommand;
    private readonly FixInstagramCommand _fixInstagramCommand;
    private readonly FixBlueskyCommand _fixBlueskyCommand;

    public ReactionAddedNotificationHandler(
        IGuildConfigurationBusinessLayer configurationBusinessLayer,
        KeywordHandler keywordHandler,
        FixTwitterCommand fixTwitterCommand,
        FixInstagramCommand fixInstagramCommand,
        FixBlueskyCommand fixBlueskyCommand)
    {
        _configurationBusinessLayer = configurationBusinessLayer;
        _keywordHandler = keywordHandler;
        _fixTwitterCommand = fixTwitterCommand;
        _fixInstagramCommand = fixInstagramCommand;
        _fixBlueskyCommand = fixBlueskyCommand;
    }
    public Task Handle(ReactionAddedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var reaction = notification.Reaction;
            var user = reaction.User.GetValueOrDefault();
            var message = await notification.Message.GetOrDownloadAsync();

            if (user is IGuildUser { IsBot: true })
            {
                return Task.CompletedTask;
            }

            bool fixingTwitter = Equals(reaction.Emote, _fixTwitterCommand.GetFixTwitterEmote());
            bool fixingInstagram = Equals(reaction.Emote, _fixInstagramCommand.GetFixInstagramEmote());
            bool fixingBluesky = Equals(reaction.Emote, _fixBlueskyCommand.GetFixBlueskyEmote());

            if (!fixingTwitter && !fixingInstagram && !fixingBluesky)
            {
                return Task.CompletedTask;
            }

            if (message == null)
            {
                return Task.CompletedTask;
            }

            ReactionMetadata? fixReaction = null;

            if (fixingTwitter)
            {
                fixReaction = message.Reactions.FirstOrDefault(r => Equals(r.Key, _fixTwitterCommand.GetFixTwitterEmote())).Value;
            }

            if (fixingInstagram)
            {
                fixReaction = message.Reactions.FirstOrDefault(r => Equals(r.Key, _fixInstagramCommand.GetFixInstagramEmote())).Value;
            }

            if (fixingBluesky)
            {
                fixReaction = message.Reactions.FirstOrDefault(r => Equals(r.Key, _fixBlueskyCommand.GetFixBlueskyEmote())).Value;
            }

            if (fixReaction == null)
            {
                return Task.CompletedTask;
            }

            if (fixReaction.Value.ReactionCount > 2)
            {
                return Task.CompletedTask;
            }
            if (notification.Reaction.Channel is not IGuildChannel guildChannel)
            {
                return Task.CompletedTask;
            }

            var config = await _configurationBusinessLayer.GetGuildConfiguration(guildChannel.Guild);
            if (fixingTwitter && config is { EnableFixTweetReactions: false }
                || fixingInstagram && config is { EnableFixInstagramReactions: false }
                || fixingBluesky && config is { EnableFixBlueskyReactions: false })
            {
                return Task.CompletedTask;
            }

            TriggerKeyword? keywordToPass = null;

            if (fixingTwitter)
            {
                if (_fixTwitterCommand.DoesMessageContainTwitterUrl(message))
                {
                    keywordToPass = TriggerKeyword.FixTwitter;
                }
                else if (_fixTwitterCommand.DoesMessageContainFxTwitterUrl(message))
                {
                    keywordToPass = TriggerKeyword.BreakTwitter;
                }
            }

            if (fixingInstagram)
            {
                if (_fixInstagramCommand.DoesMessageContainInstagramUrl(message))
                {
                    keywordToPass = TriggerKeyword.FixInstagram;
                }
                else if (_fixInstagramCommand.DoesMessageContainDdInstagramUrl(message))
                {
                    keywordToPass = TriggerKeyword.BreakInstagram;
                }
            }

            if (fixingBluesky)
            {
                if (_fixBlueskyCommand.DoesMessageContainBlueskyUrl(message))
                {
                    keywordToPass = TriggerKeyword.FixBluesky;
                }
            }

            if (keywordToPass == null)
            {
                return Task.CompletedTask;
            }

            (string fixedMessage, MessageReference messageToReplyTo)? fixedMessage = null;

            if (fixingTwitter)
            {
                fixedMessage = await _fixTwitterCommand.GetFixedTwitterMessage(message, keywordToPass.Value);
                if (fixedMessage == null || fixedMessage.Value.fixedMessage ==
                    _fixTwitterCommand.NoLinkMessage)
                {
                    return Task.CompletedTask;
                }
            }
            else if (fixingInstagram)
            {
                fixedMessage = await _fixInstagramCommand.GetFixedInstagramMessage(message, keywordToPass.Value);

                if (fixedMessage == null || fixedMessage.Value.fixedMessage == _fixInstagramCommand.NoLinkMessage)
                {
                    return Task.CompletedTask;
                }
            }
            else if (fixingBluesky)
            {
                var embeds = await _fixBlueskyCommand.GetFixedBlueskyMessage(message);

                if (!embeds.Any())
                {
                    return Task.CompletedTask;
                }

                foreach (var embedWithImage in embeds)
                {
                    if (embedWithImage.image != null)
                    {
                        var fileName = $"bsky_{DateTime.Now.ToShortDateString()}.png";
                        var fileAttachment = new FileAttachment(embedWithImage.image.Value.Stream, fileName);
                        var newEmbed = new EmbedBuilder().WithTitle(embedWithImage.embed.Title)
                            .WithDescription(embedWithImage.embed.Description)
                            .WithImageUrl($"attachment://{fileName}");
                        if (embedWithImage.embed.Footer != null)
                        {
                            newEmbed
                                .WithFooter(new EmbedFooterBuilder
                                {
                                    Text = embedWithImage.embed.Footer.Value.Text,
                                });
                        }

                        await message.Channel.SendFileAsync(fileAttachment,
                            embed: newEmbed.Build(),
                            messageReference: new MessageReference(message.Id, failIfNotExists: false));
                    }
                    else
                    {
                        await message.ReplyAsync(embed: embedWithImage.embed);
                    }
                }

                return Task.CompletedTask;
            }

            if (fixedMessage == null)
            {
                return Task.CompletedTask;
            }

            await message.ReplyAsync(fixedMessage.Value.fixedMessage);
            return Task.CompletedTask;
        }, cancellationToken);
        return Task.CompletedTask;
    }
}