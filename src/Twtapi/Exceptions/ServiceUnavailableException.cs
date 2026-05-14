using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 503 — planned or unplanned outage. Safe to retry with backoff.</summary>
public sealed class ServiceUnavailableException : TwtApiException
{
    internal ServiceUnavailableException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
