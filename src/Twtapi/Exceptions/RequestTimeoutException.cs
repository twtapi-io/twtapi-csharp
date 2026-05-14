using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 408 — the upstream did not respond in time. Safe to retry.</summary>
public sealed class RequestTimeoutException : TwtApiException
{
    internal RequestTimeoutException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
