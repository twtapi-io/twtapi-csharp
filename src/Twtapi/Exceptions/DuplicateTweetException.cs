using System.Text.Json;

namespace Twtapi;

/// <summary>
/// HTTP 422 with <c>duplicate_tweet</c> or
/// <c>tweet_silently_dropped_likely_duplicate</c> — the upstream considers
/// the text a duplicate of one you posted recently.
/// </summary>
public sealed class DuplicateTweetException : ValidationException
{
    internal DuplicateTweetException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
