using System.Text.Json;

namespace Twtapi;

/// <summary>
/// HTTP 429 — rate-limited. Read <see cref="RetryAfter"/> to back off
/// and <see cref="Scope"/> to know whether the plan or the upstream
/// account budget was hit.
/// </summary>
public sealed class RateLimitException : TwtApiException
{
    /// <summary>How long to wait before the next attempt (seconds → <see cref="TimeSpan"/>).</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// <see cref="RateLimitScope.Plan"/> when your twtapi plan limit was
    /// reached, <see cref="RateLimitScope.Account"/> when the upstream
    /// account budget was hit. <see langword="null"/> when the server
    /// omitted the field.
    /// </summary>
    public RateLimitScope? Scope { get; }

    internal RateLimitException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText,
        TimeSpan? retryAfter,
        RateLimitScope? scope)
        : base(message, status, errorCode, body, bodyText)
    {
        RetryAfter = retryAfter;
        Scope = scope;
    }
}
