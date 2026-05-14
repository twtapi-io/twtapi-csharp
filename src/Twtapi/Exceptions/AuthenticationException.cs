using System.Text.Json;

namespace Twtapi;

/// <summary>HTTP 401 — the <c>X-API-Key</c> is missing or invalid.</summary>
public sealed class AuthenticationException : TwtApiException
{
    internal AuthenticationException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
