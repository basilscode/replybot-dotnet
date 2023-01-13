﻿using System.Text.RegularExpressions;

namespace Replybot.TextCommands;

public class FixTwitterCommand
{
    private const string TwitterUrlRegexPattern = "https?:\\/\\/(www.)?(twitter.com|t.co)\\/[a-z0-9_]+\\/status\\/[0-9]+";
    private readonly Regex _twitterUrlRegex = new(TwitterUrlRegexPattern, RegexOptions.IgnoreCase);

    public async Task<(string fixedTwitterMessage, MessageReference messageToReplyTo)?> GetFixedTwitterMessage(ISocketMessageChannel channel, SocketMessage message)
    {
        IMessage messageToFix = message;

        if (message.Reference == null)
        {
            return DoesMessageContainTwitterUrl(messageToFix)
                ? (BuildFixedTweetsMessage(messageToFix), new MessageReference(messageToFix.Id))
                : ("I don't think there's a twitter link there.", new MessageReference(message.Id));
        }

        var messageReferenceId = message.Reference.MessageId.GetValueOrDefault(default);
        if (messageReferenceId == default)
        {
            return (string.Join("\n", FixTwitterUrls(message)), new MessageReference(message.Id));
        }

        var messageReferenced = await message.Channel.GetMessageAsync(messageReferenceId);
        if (messageReferenced is not { } referencedSocketMessage)
        {
            return ("I couldn't read that message for some reason, sorry!", new MessageReference(message.Id));
        }

        messageToFix = referencedSocketMessage;

        return DoesMessageContainTwitterUrl(messageToFix)
            ? (BuildFixedTweetsMessage(messageToFix), new MessageReference(messageToFix.Id))
            : ("I don't think there's a twitter link there.", new MessageReference(message.Id));
    }

    private string BuildFixedTweetsMessage(IMessage message)
    {
        var fixedTweets = FixTwitterUrls(message);
        var tweetDescribeText = fixedTweets.Count == 1 ? "this tweet" : "these tweets";
        var authorMentionMessage = $"{message.Author.Mention} asked me to fix {tweetDescribeText}:\n";
        return $"{authorMentionMessage}{string.Join("\n", fixedTweets)}";
    }

    private bool DoesMessageContainTwitterUrl(IMessage messageReferenced)
    {
        return _twitterUrlRegex.IsMatch(messageReferenced.Content);
    }

    private IList<string> FixTwitterUrls(IMessage messageToFix)
    {
        var urlsFromMessage = GetTwitterUrlsFromMessage(messageToFix.Content);
        return urlsFromMessage.Select(url => url.Replace("twitter.com", "fxtwitter.com", StringComparison.InvariantCultureIgnoreCase)).ToList();
    }

    private IEnumerable<string> GetTwitterUrlsFromMessage(string text)
    {
        var matches = _twitterUrlRegex.Matches(text);
        return matches.Select(t => t.Value).ToList();
    }
}