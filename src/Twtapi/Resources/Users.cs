using System.Text.Json;
using Twtapi.Pagination;

namespace Twtapi.Resources;

/// <summary>
/// User lookups, followers, tweets timeline, and follow action.
/// </summary>
/// <remarks>
/// Numeric identifiers (<c>userId</c>) are <see cref="string"/> rather
/// than <see cref="long"/> to preserve full 64-bit precision and match
/// the wire format.
/// </remarks>
public sealed class Users
{
    private readonly Transport.Transport _transport;

    internal Users(Transport.Transport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Fetch a user's full profile by handle. <c>GET /user</c>
    /// </summary>
    /// <param name="username">Account handle without the <c>@</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetAsync(string username, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/user",
            parameters: new Dictionary<string, object?> { ["username"] = username },
            ct: ct);

    /// <summary>Resolve a handle to a numeric <c>user_id</c>. <c>GET /id_by_username</c></summary>
    /// <param name="username">Account handle without the <c>@</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/id_by_username",
            parameters: new Dictionary<string, object?> { ["username"] = username },
            ct: ct);

    /// <summary>Resolve a numeric <c>user_id</c> to a handle. <c>GET /username_by_id</c></summary>
    /// <param name="userId">Numeric user identifier as a string.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetByIdAsync(string userId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/username_by_id",
            parameters: new Dictionary<string, object?> { ["user_id"] = userId },
            ct: ct);

    /// <summary>
    /// One page of followers. <c>GET /followers</c>
    /// </summary>
    /// <remarks>
    /// Server returns items under <c>followers[]</c> (not <c>users[]</c>).
    /// </remarks>
    /// <param name="userId">Numeric user identifier.</param>
    /// <param name="count">Page size. Default 20, max 200.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetFollowersAsync(
        string userId,
        int? count = null,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/followers",
            parameters: new Dictionary<string, object?>
            {
                ["user_id"] = userId,
                ["count"] = count,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>Iterate every follower across pages. Honours <paramref name="maxPages"/> and <paramref name="maxItems"/>.</summary>
    /// <param name="userId">Numeric user identifier.</param>
    /// <param name="count">Per-page size.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many items, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<JsonElement> IterateFollowersAsync(
        string userId,
        int? count = null,
        int? maxPages = null,
        int? maxItems = null,
        CancellationToken ct = default) =>
        AsyncPaginator.IterateItemsAsync(
            fetch: (cursor, token) => GetFollowersAsync(userId, count, cursor, token),
            itemsField: "followers",
            cursorField: "cursor_bottom",
            maxPages: maxPages,
            maxItems: maxItems,
            ct: ct);

    /// <summary>One page of a user's tweets. <c>GET /user_tweets</c></summary>
    /// <param name="userId">Numeric user identifier.</param>
    /// <param name="count">Page size. Default 20, max 200.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetTweetsAsync(
        string userId,
        int? count = null,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/user_tweets",
            parameters: new Dictionary<string, object?>
            {
                ["user_id"] = userId,
                ["count"] = count,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>Iterate every tweet of a user across pages.</summary>
    /// <param name="userId">Numeric user identifier.</param>
    /// <param name="count">Per-page size.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many items, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<JsonElement> IterateTweetsAsync(
        string userId,
        int? count = null,
        int? maxPages = null,
        int? maxItems = null,
        CancellationToken ct = default) =>
        AsyncPaginator.IterateItemsAsync(
            fetch: (cursor, token) => GetTweetsAsync(userId, count, cursor, token),
            itemsField: "tweets",
            cursorField: "cursor_bottom",
            maxPages: maxPages,
            maxItems: maxItems,
            ct: ct);

    /// <summary>Follow a user from the cookie owner's account. <c>POST /follow</c>. Requires engagement cookies.</summary>
    /// <param name="userId">Numeric user_id of the account to follow.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> FollowAsync(string userId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/follow",
            jsonBody: new Dictionary<string, object?> { ["user_id"] = userId },
            sendCookies: true,
            ct: ct);
}
