using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 404 — the target resource does not exist or is not visible.</summary>
public sealed class NotFoundException : TwtApiException
{
    internal NotFoundException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
