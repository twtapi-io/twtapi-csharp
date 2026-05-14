namespace Twtapi;

/// <summary>
/// Engagement cookie state — holds <c>auth_token</c> + <c>ct0</c>, exposes
/// them to every authenticated call, and auto-rotates <c>ct0</c> when the
/// server returns a fresh value in the <c>X-Twitter-New-Ct0</c> response
/// header.
/// </summary>
/// <remarks>
/// Reads (<see cref="AuthToken"/>, <see cref="Ct0"/>) are atomic. Writes
/// are guarded by an internal lock so concurrent rotations from
/// overlapping requests are race-safe.
/// </remarks>
public sealed class CookieState
{
    private readonly object _lock = new();
    private string? _authToken;
    private string? _ct0;

    internal CookieState(string? authToken = null, string? ct0 = null)
    {
        _authToken = authToken;
        _ct0 = ct0;
    }

    /// <summary>Current <c>auth_token</c>, or <see langword="null"/> if not set.</summary>
    public string? AuthToken
    {
        get { lock (_lock) return _authToken; }
    }

    /// <summary>Current <c>ct0</c>, updated automatically on rotation.</summary>
    public string? Ct0
    {
        get { lock (_lock) return _ct0; }
    }

    /// <summary>Atomic snapshot of the held pair, for header injection.</summary>
    internal (string? AuthToken, string? Ct0) Snapshot()
    {
        lock (_lock) return (_authToken, _ct0);
    }

    /// <summary>Replace the held pair (called by <c>SetCookies</c> and <c>ChangePassword</c>).</summary>
    internal void Set(string? authToken, string? ct0)
    {
        lock (_lock)
        {
            _authToken = authToken;
            _ct0 = ct0;
        }
    }

    /// <summary>
    /// Rotate <c>ct0</c> in place. Returns the new value when it actually
    /// changed (so the transport can fire the rotation event exactly once),
    /// or <see langword="null"/> when the value was empty or unchanged.
    /// </summary>
    internal string? RotateCt0(string newCt0)
    {
        if (string.IsNullOrEmpty(newCt0)) return null;
        lock (_lock)
        {
            if (newCt0 == _ct0) return null;
            _ct0 = newCt0;
            return newCt0;
        }
    }
}
