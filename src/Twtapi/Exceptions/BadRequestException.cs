using System.Text.Json;

namespace Twtapi;

/// <summary>
/// HTTP 400 — the request was malformed (missing parameter, wrong type,
/// invalid JSON).
/// </summary>
public sealed class BadRequestException : TwtApiException
{
    internal BadRequestException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
