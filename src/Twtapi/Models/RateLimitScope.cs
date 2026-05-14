namespace Twtapi;

/// <summary>
/// Which limit was hit on a 429 response.
/// </summary>
public enum RateLimitScope
{
    /// <summary>Your twtapi plan ceiling was reached.</summary>
    Plan,

    /// <summary>The acting account's upstream budget was reached.</summary>
    Account,
}
