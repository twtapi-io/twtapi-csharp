using System.Text.Json;

namespace Twtapi;

/// <summary>
/// HTTP 403 — the acting account is allowed to authenticate but not to
/// perform this action. Common reasons: <c>engagement_cookies_required</c>,
/// <c>account_not_activated</c>.
/// </summary>
public sealed class PermissionException : TwtApiException
{
    internal PermissionException(
        string message,
        int? status,
        string? errorCode,
        JsonElement? body,
        string? bodyText)
        : base(message, status, errorCode, body, bodyText) { }
}
