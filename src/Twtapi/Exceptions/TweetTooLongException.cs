using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 422 with <c>tweet_too_long</c> — the text exceeds the per-tweet limit.</summary>
public sealed class TweetTooLongException : ValidationException
{
    internal TweetTooLongException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
