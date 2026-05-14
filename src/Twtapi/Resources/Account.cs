using System.Text.Json;

namespace Twtapi.Resources;

/// <summary>
/// Account-level mutations (currently: password change).
/// </summary>
/// <remarks>
/// Password change invalidates the previous session. The SDK auto-rotates
/// the held <c>auth_token</c> + <c>ct0</c> pair so subsequent engagement
/// calls keep working.
/// </remarks>
public sealed class Account
{
    private readonly Transport.Transport _transport;
    private readonly CookieState _cookies;

    internal Account(Transport.Transport transport, CookieState cookies)
    {
        _transport = transport;
        _cookies = cookies;
    }

    /// <summary>
    /// Change the cookie owner's account password. <c>POST /change_password</c>.
    /// Requires engagement cookies.
    /// </summary>
    /// <remarks>
    /// Pass <paramref name="newPassword"/> as <see langword="null"/> (or
    /// empty) to have a 16-char password generated server-side. The
    /// response carries <c>new_auth_token</c> + <c>new_ct0</c> — the SDK
    /// rotates the held cookie pair automatically.
    /// </remarks>
    /// <param name="current">Current account password.</param>
    /// <param name="newPassword">New password (8–128 chars), or <see langword="null"/> for a generated one.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<JsonElement> ChangePasswordAsync(
        string current,
        string? newPassword = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["current_password"] = current };
        if (!string.IsNullOrEmpty(newPassword))
            payload["password"] = newPassword;

        JsonElement response = await _transport.RequestAsync(
            HttpMethod.Post,
            "/change_password",
            jsonBody: payload,
            sendCookies: true,
            ct: ct).ConfigureAwait(false);

        if (response.ValueKind == JsonValueKind.Object)
        {
            string? newAuth = TryReadString(response, "new_auth_token");
            string? newCt0 = TryReadString(response, "new_ct0");
            if (!string.IsNullOrEmpty(newAuth) && !string.IsNullOrEmpty(newCt0))
                _cookies.Set(newAuth, newCt0);
        }

        return response;
    }

    private static string? TryReadString(JsonElement obj, string field) =>
        obj.TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
