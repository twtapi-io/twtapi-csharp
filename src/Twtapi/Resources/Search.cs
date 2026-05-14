using System.Text.Json;
using Twtapi.Pagination;

namespace Twtapi.Resources;

/// <summary>
/// Search tweets — <c>GET /search</c>.
/// </summary>
public sealed class Search
{
    private readonly Transport.Transport _transport;

    internal Search(Transport.Transport transport)
    {
        _transport = transport;
    }

    /// <summary>One page of search results. <c>GET /search</c></summary>
    /// <param name="query">Search query. Supports operators like <c>from:</c>, <c>since:</c>, <c>lang:</c>.</param>
    /// <param name="product">Ranking surface. Defaults to <see cref="SearchProduct.Top"/> server-side.</param>
    /// <param name="count">Page size. Default 20, max 200.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> QueryAsync(
        string query,
        SearchProduct? product = null,
        int? count = null,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/search",
            parameters: new Dictionary<string, object?>
            {
                ["q"] = query,
                ["product"] = product?.ToString(),
                ["count"] = count,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>Iterate every search result across pages.</summary>
    /// <param name="query">Search query.</param>
    /// <param name="product">Ranking surface.</param>
    /// <param name="count">Per-page size.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many results, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<JsonElement> IterateAsync(
        string query,
        SearchProduct? product = null,
        int? count = null,
        int? maxPages = null,
        int? maxItems = null,
        CancellationToken ct = default) =>
        AsyncPaginator.IterateItemsAsync(
            fetch: (cursor, token) => QueryAsync(query, product, count, cursor, token),
            itemsField: "tweets",
            cursorField: "cursor_bottom",
            maxPages: maxPages,
            maxItems: maxItems,
            ct: ct);
}
