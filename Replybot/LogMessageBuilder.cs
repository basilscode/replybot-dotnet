﻿using Replybot.Models;

namespace Replybot
{
    public class LogMessageBuilder
    {
        private readonly DiscordSettings _discordSettings;
        private const string TruncationString = "[...]";

        public LogMessageBuilder(DiscordSettings discordSettings)
        {
            _discordSettings = discordSettings;
        }

        public EmbedBuilder CreateEmbedBuilder(string title, string explanationMessage, IMessage message)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle(title);

            HandleEmbeds(explanationMessage, message, embedBuilder);

            return embedBuilder;
        }

        private void HandleEmbeds(string explanationMessage, IMessage message, EmbedBuilder embedBuilder)
        {
            var embedDescription = "";

            if (message.Embeds.Any())
            {
                var embed = message.Embeds.First();
                if (embed.Image.HasValue)
                {
                    embedBuilder.WithImageUrl(embed.Image.Value.Url);
                }

                embedDescription = embed.Description;
            }

            if (message.Attachments.Any())
            {
                embedBuilder.AddField("Attachments",
                    string.Join(", ",
                        message.Attachments.Select((attachment, index) => $"[Attachment {index + 1}]({attachment.Url})")));
            }

            var descriptionToUse = !string.IsNullOrEmpty(explanationMessage) ? $"**{explanationMessage}**" : "";
            descriptionToUse += !string.IsNullOrEmpty(message.Content) ? $"\n{message.Content}" : "";
            descriptionToUse += !string.IsNullOrEmpty(embedDescription) ? $"\n-----------\n{embedDescription}" : "";

            embedBuilder.WithDescription(descriptionToUse);

            if (embedBuilder.Description.Length <= _discordSettings.MaxCharacters)
            {
                return;
            }
            var maxCharacters = _discordSettings.MaxCharacters - TruncationString.Length;
            embedBuilder.WithDescription($"{embedBuilder.Description[..maxCharacters]}{TruncationString}");
        }

        public EmbedBuilder CreateEmbedBuilderWithFields(string title, string description,
            Dictionary<string, string> messagesToEmbed)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description);

            embedBuilder.WithFields(messagesToEmbed.Select(m => new EmbedFieldBuilder
            {
                Name = m.Key,
                Value = TruncateIfLongerThanMaxCharacters(m.Value, _discordSettings.MaxCharacters, TruncationString),
                IsInline = false
            }));

            return embedBuilder;
        }

        private static string TruncateIfLongerThanMaxCharacters(string messageContent, int maxCharacters, string truncationString)
        {
            if (messageContent.Length <= maxCharacters)
            {
                return messageContent;
            }
            var maxCharactersAllowed = maxCharacters - truncationString.Length;
            return $"{messageContent[..maxCharactersAllowed]}{truncationString}";
        }
    }
}
