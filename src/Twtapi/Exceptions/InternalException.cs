using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 500 — unexpected server-side failure. Safe to retry with backoff.</summary>
public sealed class InternalException : TwtApiException
{
    internal InternalException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
