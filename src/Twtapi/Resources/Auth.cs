using System.Text.Json;

namespace Twtapi.Resources;

/// <summary>
/// Login flow, CSRF refresh, and whoami helper.
/// </summary>
/// <remarks>
/// <see cref="LoginAsync"/> and the continuation methods return a
/// <see cref="LoginResult"/> discriminated union — pattern-match on the
/// concrete subtype.
/// </remarks>
public sealed class Auth
{
    private readonly Transport.Transport _transport;

    internal Auth(Transport.Transport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Start a login. <c>POST /login/start</c>
    /// </summary>
    /// <param name="username">𝕏 username or email.</param>
    /// <param name="password">Account password.</param>
    /// <param name="proxy">Optional outbound proxy used for the upstream login request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="LoginResult.Ok"/> when login completes immediately,
    /// <see cref="LoginResult.Challenge"/> when an extra verification step
    /// is required (pass <c>State</c> to <see cref="Submit2FAAsync"/> or
    /// <see cref="SubmitEmailCodeAsync"/>), or <see cref="LoginResult.Error"/>
    /// on terminal failure.
    /// </returns>
    public async Task<LoginResult> LoginAsync(
        string username,
        string password,
        string? proxy = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["username"] = username,
            ["password"] = password,
        };
        if (!string.IsNullOrEmpty(proxy)) payload["proxy"] = proxy;

        JsonElement body = await _transport.RequestAsync(
            HttpMethod.Post,
            "/login/start",
            jsonBody: payload,
            ct: ct).ConfigureAwait(false);

        return Parse(body);
    }

    /// <summary>Submit a TOTP / authenticator code to continue a login. <c>POST /login/2fa</c></summary>
    /// <param name="challengeToken">The opaque <c>state</c> from the previous step.</param>
    /// <param name="code">Six-digit code from the authenticator app.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<LoginResult> Submit2FAAsync(
        string challengeToken,
        string code,
        CancellationToken ct = default)
    {
        var body = await _transport.RequestAsync(
            HttpMethod.Post,
            "/login/2fa",
            jsonBody: new Dictionary<string, object?>
            {
                ["state"] = challengeToken,
                ["code"] = code,
            },
            ct: ct).ConfigureAwait(false);
        return Parse(body);
    }

    /// <summary>Submit an email / SMS verification code. <c>POST /login/email_code</c></summary>
    /// <param name="challengeToken">The opaque <c>state</c> from the previous step.</param>
    /// <param name="code">Code from the email / SMS message.</param>
    /// <param name="alternateId">Alternative identifier some flows require. Usually <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<LoginResult> SubmitEmailCodeAsync(
        string challengeToken,
        string code,
        string? alternateId = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["state"] = challengeToken,
            ["code"] = code,
        };
        if (alternateId is not null) payload["alternate_id"] = alternateId;
        var body = await _transport.RequestAsync(
            HttpMethod.Post,
            "/login/email_code",
            jsonBody: payload,
            ct: ct).ConfigureAwait(false);
        return Parse(body);
    }

    /// <summary>
    /// Mint a fresh <c>ct0</c> from an <c>auth_token</c>. <c>GET /csrf_token</c>.
    /// Only the <c>X-Twitter-Auth-Token</c> header is sent — <c>ct0</c> is the response.
    /// </summary>
    /// <param name="authToken">An <c>auth_token</c> cookie value.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> CsrfTokenAsync(string authToken, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/csrf_token",
            extraHeaders: new Dictionary<string, string>
            {
                ["X-Twitter-Auth-Token"] = authToken,
            },
            ct: ct);

    /// <summary>Identify the account behind a cookie pair. <c>GET /screen_name_from_token</c></summary>
    /// <param name="authToken">An <c>auth_token</c> cookie value.</param>
    /// <param name="ct0">Companion <c>ct0</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> WhoAmIAsync(string authToken, string ct0, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/screen_name_from_token",
            extraHeaders: new Dictionary<string, string>
            {
                ["X-Twitter-Auth-Token"] = authToken,
                ["X-Twitter-Ct0"] = ct0,
            },
            ct: ct);

    private static LoginResult Parse(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
            return new LoginResult.Error("Unexpected response shape.");

        string status = body.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString() ?? string.Empty
            : string.Empty;

        switch (status)
        {
            case "ok":
                {
                    string authToken = ReadString(body, "auth_token") ?? string.Empty;
                    string ct0 = ReadString(body, "ct0") ?? string.Empty;
                    return new LoginResult.Ok(authToken, ct0);
                }
            case "challenge":
                {
                    string type = ReadString(body, "type") ?? string.Empty;
                    string state = ReadString(body, "state") ?? string.Empty;
                    return new LoginResult.Challenge(type, state);
                }
            default:
                {
                    string message = ReadString(body, "message") ?? $"Login failed (status={status}).";
                    return new LoginResult.Error(message);
                }
        }
    }

    private static string? ReadString(JsonElement body, string field) =>
        body.TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
