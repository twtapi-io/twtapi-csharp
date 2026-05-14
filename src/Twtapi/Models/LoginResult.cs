namespace Twtapi;

/// <summary>
/// Discriminated-union-style result for <see cref="Resources.Auth.LoginAsync"/>
/// (and its 2FA / email-code continuations).
/// </summary>
/// <remarks>
/// <para>
/// Use pattern matching on the concrete subtype:
/// </para>
/// <example>
/// <code>
/// switch (await client.Auth.LoginAsync("handle", "password"))
/// {
///     case LoginResult.Ok ok:
///         client.SetCookies(ok.AuthToken, ok.Ct0);
///         break;
///     case LoginResult.Challenge ch:
///         var code = ReadCode();
///         var next = await client.Auth.Submit2FAAsync(ch.State, code);
///         // handle `next` (Ok / Challenge / Error) the same way.
///         break;
///     case LoginResult.Error err:
///         Console.Error.WriteLine(err.Message);
///         break;
/// }
/// </code>
/// </example>
/// </remarks>
public abstract record LoginResult
{
    // Sealed subtypes only — consumers cannot extend this hierarchy.
    private LoginResult() { }

    /// <summary>Login completed. Persist <see cref="AuthToken"/> + <see cref="Ct0"/>.</summary>
    /// <param name="AuthToken">Fresh <c>auth_token</c> for this session.</param>
    /// <param name="Ct0">Companion <c>ct0</c> CSRF token.</param>
    public sealed record Ok(string AuthToken, string Ct0) : LoginResult;

    /// <summary>
    /// The upstream challenged the login. Pass <see cref="State"/> back to the
    /// matching continuation: <see cref="Resources.Auth.Submit2FAAsync"/>
    /// when <see cref="Type"/> is <c>"two_factor"</c>, or
    /// <see cref="Resources.Auth.SubmitEmailCodeAsync"/> otherwise.
    /// </summary>
    /// <param name="Type">Challenge kind, e.g. <c>"two_factor"</c> or <c>"email_code"</c>.</param>
    /// <param name="State">Opaque encrypted continuation token.</param>
    public sealed record Challenge(string Type, string State) : LoginResult;

    /// <summary>Terminal failure — the login cannot continue.</summary>
    /// <param name="Message">Server-supplied error message.</param>
    public sealed record Error(string Message) : LoginResult;
}
