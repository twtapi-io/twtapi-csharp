using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 502 — upstream gateway error. Safe to retry with backoff.</summary>
public sealed class UpstreamException : TwtApiException
{
    internal UpstreamException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
