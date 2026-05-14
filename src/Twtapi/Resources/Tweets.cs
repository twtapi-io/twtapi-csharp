using System.Text.Json;
using Twtapi.Pagination;

namespace Twtapi.Resources;

/// <summary>
/// Tweet reads (retweets, quotes, comments, reply IDs) and engagement
/// mutations.
/// </summary>
/// <remarks>
/// <para>Per the public API:</para>
/// <list type="bullet">
///   <item><c>/retweets</c> returns compact users under <c>users[]</c>.</item>
///   <item><c>/quotes</c>, <c>/user_tweets</c>, <c>/search</c> return tweets under <c>tweets[]</c>.</item>
///   <item><c>/comments</c> returns reply tweets under <c>comments[]</c>.</item>
///   <item><c>/reply_ids</c> returns string IDs under <c>reply_ids[]</c>.</item>
///   <item><c>POST /tweet</c> and <c>POST /comment</c> accept either <c>media_id</c> (single) or <c>media_ids</c> (array, up to 4).</item>
/// </list>
/// <para>
/// Numeric identifiers (<c>tweetId</c>) are <see cref="string"/> rather
/// than <see cref="long"/> to preserve full 64-bit precision.
/// </para>
/// </remarks>
public sealed class Tweets
{
    private readonly Transport.Transport _transport;

    internal Tweets(Transport.Transport transport)
    {
        _transport = transport;
    }

    // -------------------------------------------------------------- reads

    /// <summary>Users who retweeted a tweet. <c>GET /retweets</c></summary>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="count">Page size. Default 20, max 200.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetRetweetsAsync(
        string tweetId,
        int? count = null,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/retweets",
            parameters: new Dictionary<string, object?>
            {
                ["tweet_id"] = tweetId,
                ["count"] = count,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>Iterate every user who retweeted a tweet.</summary>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="count">Per-page size.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many items, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<JsonElement> IterateRetweetsAsync(
        string tweetId,
        int? count = null,
        int? maxPages = null,
        int? maxItems = null,
        CancellationToken ct = default) =>
        AsyncPaginator.IterateItemsAsync(
            fetch: (cursor, token) => GetRetweetsAsync(tweetId, count, cursor, token),
            itemsField: "users",
            maxPages: maxPages,
            maxItems: maxItems,
            ct: ct);

    /// <summary>Quote tweets of a tweet. <c>GET /quotes</c></summary>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="count">Page size. Default 20, max 200.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetQuotesAsync(
        string tweetId,
        int? count = null,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/quotes",
            parameters: new Dictionary<string, object?>
            {
                ["tweet_id"] = tweetId,
                ["count"] = count,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>Iterate every quote tweet of a tweet.</summary>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="count">Per-page size.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many items, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<JsonElement> IterateQuotesAsync(
        string tweetId,
        int? count = null,
        int? maxPages = null,
        int? maxItems = null,
        CancellationToken ct = default) =>
        AsyncPaginator.IterateItemsAsync(
            fetch: (cursor, token) => GetQuotesAsync(tweetId, count, cursor, token),
            itemsField: "tweets",
            maxPages: maxPages,
            maxItems: maxItems,
            ct: ct);

    /// <summary>Hydrated replies to a tweet. <c>GET /comments</c></summary>
    /// <remarks>For ID-only traversal, prefer <see cref="GetReplyIdsAsync"/>.</remarks>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetCommentsAsync(
        string tweetId,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/comments",
            parameters: new Dictionary<string, object?>
            {
                ["tweet_id"] = tweetId,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>Iterate every reply to a tweet (hydrated).</summary>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many items, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<JsonElement> IterateCommentsAsync(
        string tweetId,
        int? maxPages = null,
        int? maxItems = null,
        CancellationToken ct = default) =>
        AsyncPaginator.IterateItemsAsync(
            fetch: (cursor, token) => GetCommentsAsync(tweetId, cursor, token),
            itemsField: "comments",
            maxPages: maxPages,
            maxItems: maxItems,
            ct: ct);

    /// <summary>Reply IDs only (cheaper than <see cref="GetCommentsAsync"/>). <c>GET /reply_ids</c></summary>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetReplyIdsAsync(
        string tweetId,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/reply_ids",
            parameters: new Dictionary<string, object?>
            {
                ["tweet_id"] = tweetId,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>Iterate every reply ID (as a string) for a tweet.</summary>
    /// <param name="tweetId">Tweet identifier.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many IDs, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public IAsyncEnumerable<string> IterateReplyIdsAsync(
        string tweetId,
        int? maxPages = null,
        int? maxItems = null,
        CancellationToken ct = default) =>
        AsyncPaginator.IterateStringItemsAsync(
            fetch: (cursor, token) => GetReplyIdsAsync(tweetId, cursor, token),
            itemsField: "reply_ids",
            maxPages: maxPages,
            maxItems: maxItems,
            ct: ct);

    // ------------------------------------------------------------- writes

    /// <summary>
    /// Post a tweet. <c>POST /tweet</c>. Requires engagement cookies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CreateTweetOptions.InReplyTo"/> and
    /// <see cref="CreateTweetOptions.AttachmentUrl"/> are mutually
    /// exclusive. Attach media via <see cref="CreateTweetOptions.MediaId"/>
    /// (single) or <see cref="CreateTweetOptions.MediaIds"/> (up to four).
    /// </para>
    /// </remarks>
    /// <param name="text">Tweet text. Up to 280 characters.</param>
    /// <param name="options">Optional reply / quote / media parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> CreateAsync(
        string text,
        CreateTweetOptions? options = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["text"] = text };
        if (options?.InReplyTo is not null) payload["in_reply_to"] = options.InReplyTo;
        if (options?.AttachmentUrl is not null) payload["attachment_url"] = options.AttachmentUrl;
        AttachMedia(payload, options?.MediaId, options?.MediaIds);
        return _transport.RequestAsync(
            HttpMethod.Post,
            "/tweet",
            jsonBody: payload,
            sendCookies: true,
            ct: ct);
    }

    /// <summary>Reply to a tweet. <c>POST /comment</c>. Requires engagement cookies.</summary>
    /// <param name="tweetId">Tweet to reply to.</param>
    /// <param name="text">Reply text.</param>
    /// <param name="options">Optional media parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> CommentAsync(
        string tweetId,
        string text,
        CommentOptions? options = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["tweet_id"] = tweetId, ["text"] = text };
        AttachMedia(payload, options?.MediaId, options?.MediaIds);
        return _transport.RequestAsync(
            HttpMethod.Post,
            "/comment",
            jsonBody: payload,
            sendCookies: true,
            ct: ct);
    }

    /// <summary>Like a tweet. <c>POST /like</c>. Requires engagement cookies.</summary>
    /// <param name="tweetId">Tweet to like.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> LikeAsync(string tweetId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/like",
            jsonBody: new Dictionary<string, object?> { ["tweet_id"] = tweetId },
            sendCookies: true,
            ct: ct);

    /// <summary>Retweet a tweet. <c>POST /retweet</c>. Requires engagement cookies.</summary>
    /// <param name="tweetId">Tweet to retweet.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> RetweetAsync(string tweetId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/retweet",
            jsonBody: new Dictionary<string, object?> { ["tweet_id"] = tweetId },
            sendCookies: true,
            ct: ct);

    /// <summary>Bookmark a tweet. <c>POST /bookmark</c>. Requires engagement cookies.</summary>
    /// <param name="tweetId">Tweet to bookmark.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> BookmarkAsync(string tweetId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/bookmark",
            jsonBody: new Dictionary<string, object?> { ["tweet_id"] = tweetId },
            sendCookies: true,
            ct: ct);

    /// <summary>Delete one of the cookie owner's tweets. <c>POST /delete_tweet</c>. Requires engagement cookies.</summary>
    /// <param name="tweetId">Tweet to delete. Must be authored by the cookie owner.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> DeleteAsync(string tweetId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/delete_tweet",
            jsonBody: new Dictionary<string, object?> { ["tweet_id"] = tweetId },
            sendCookies: true,
            ct: ct);

    private static void AttachMedia(
        Dictionary<string, object?> payload,
        string? mediaId,
        IReadOnlyList<string>? mediaIds)
    {
        if (mediaId is not null) payload["media_id"] = mediaId;
        if (mediaIds is { Count: > 0 }) payload["media_ids"] = mediaIds;
    }
}
