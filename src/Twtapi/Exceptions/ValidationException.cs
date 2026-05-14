using System.Text.Json;

namespace Twtapi;

/// <summary>
/// HTTP 422 — the upstream rejected the request as semantically invalid.
/// </summary>
/// <remarks>
/// Specific 422 reasons surface as derived classes that callers can match
/// directly:
/// <list type="bullet">
///   <item><see cref="DuplicateTweetException"/></item>
///   <item><see cref="TweetTooLongException"/></item>
/// </list>
/// </remarks>
public class ValidationException : TwtApiException
{
    internal ValidationException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
