using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Twtapi.Pagination;

/// <summary>
/// Cursor-based pagination helpers used by every iterator on the SDK.
/// </summary>
/// <remarks>
/// Most read endpoints return:
/// <code>{ count, cursor_top?, cursor_bottom, &lt;itemsField&gt;: T[] }</code>
/// <para>
/// <see cref="IteratePagesAsync"/> walks pages by calling a fetcher with
/// the current cursor; <see cref="IterateItemsAsync"/> flattens them into
/// individual items. Both honour <c>maxPages</c> and <c>maxItems</c> caps
/// so callers can bound long walks defensively.
/// </para>
/// <para>
/// <c>/community_members</c> uses <c>next_cursor</c> instead of
/// <c>cursor_bottom</c> — pass <c>cursorField: "next_cursor"</c> there.
/// </para>
/// </remarks>
internal static class AsyncPaginator
{
    /// <summary>Fetcher signature: given a cursor (null on first page) → response page.</summary>
    public delegate Task<JsonElement> PageFetcher(string? cursor, CancellationToken ct);

    /// <summary>Walk pages until the cursor is empty or one of the caps is reached.</summary>
    public static async IAsyncEnumerable<JsonElement> IteratePagesAsync(
        PageFetcher fetch,
        string cursorField = "cursor_bottom",
        int? maxPages = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string? cursor = null;
        int seen = 0;
        while (true)
        {
            JsonElement page = await fetch(cursor, ct).ConfigureAwait(false);
            yield return page;
            seen += 1;
            if (maxPages is { } cap && seen >= cap) yield break;

            string? next = ExtractCursor(page, cursorField);
            if (string.IsNullOrEmpty(next) || next == cursor) yield break;
            cursor = next;
        }
    }

    /// <summary>Walk every item in <c>itemsField</c> across pages.</summary>
    public static async IAsyncEnumerable<JsonElement> IterateItemsAsync(
        PageFetcher fetch,
        string itemsField,
        string cursorField = "cursor_bottom",
        int? maxPages = null,
        int? maxItems = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int yielded = 0;
        await foreach (var page in IteratePagesAsync(fetch, cursorField, maxPages, ct).ConfigureAwait(false))
        {
            if (page.ValueKind != JsonValueKind.Object) continue;
            if (!page.TryGetProperty(itemsField, out var array) || array.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in array.EnumerateArray())
            {
                yield return item;
                yielded += 1;
                if (maxItems is { } cap && yielded >= cap) yield break;
            }
        }
    }

    /// <summary>Walk every string item in <c>itemsField</c> across pages.</summary>
    public static async IAsyncEnumerable<string> IterateStringItemsAsync(
        PageFetcher fetch,
        string itemsField,
        string cursorField = "cursor_bottom",
        int? maxPages = null,
        int? maxItems = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int yielded = 0;
        await foreach (var page in IteratePagesAsync(fetch, cursorField, maxPages, ct).ConfigureAwait(false))
        {
            if (page.ValueKind != JsonValueKind.Object) continue;
            if (!page.TryGetProperty(itemsField, out var array) || array.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                string? value = item.GetString();
                if (value is null) continue;
                yield return value;
                yielded += 1;
                if (maxItems is { } cap && yielded >= cap) yield break;
            }
        }
    }

    private static string? ExtractCursor(JsonElement page, string field)
    {
        if (page.ValueKind != JsonValueKind.Object) return null;
        if (!page.TryGetProperty(field, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }
}
