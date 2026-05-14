namespace Twtapi;

/// <summary>
/// Snapshot of the <c>X-RateLimit-*</c> headers from the most recent
/// successful response.
/// </summary>
/// <remarks>
/// Fields:
/// <list type="bullet">
///   <item><c>X-RateLimit-Limit</c> — steady-state RPS for the matched bucket.</item>
///   <item><c>X-RateLimit-Remaining</c> — requests left in the current window.</item>
///   <item><c>X-RateLimit-Reset</c> — Unix timestamp when the window resets.</item>
/// </list>
/// Only <c>Remaining</c> is guaranteed to be present; the other two
/// fields are <see langword="null"/> when the server omits them.
/// </remarks>
/// <param name="Limit">Steady-state RPS for the matched bucket, when reported.</param>
/// <param name="Remaining">Requests left in the current window, when reported.</param>
/// <param name="Reset">Unix timestamp at which the window resets, when reported.</param>
public sealed record RateLimit(int? Limit, int? Remaining, long? Reset);
