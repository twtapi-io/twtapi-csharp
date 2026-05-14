using System.Runtime.CompilerServices;
using System.Text.Json;
using Twtapi.Pagination;

namespace Twtapi.Resources;

/// <summary>
/// Community lookups, membership checks, and join / leave actions.
/// </summary>
/// <remarks>
/// <para>
/// Every community endpoint is viewer-scoped — it reflects the caller's
/// relationship with the community, not a global truth. <c>info</c>,
/// <c>checkMember</c>, and the three write actions require engagement
/// cookies; <c>members</c> does not.
/// </para>
/// <para>
/// <c>members</c> paginates with <c>next_cursor</c> (not
/// <c>cursor_bottom</c>) and returns members grouped by role under
/// <c>members_by_role</c> (e.g. <c>Admin</c>, <c>Member</c>). The
/// <see cref="IterateMembersAsync"/> helper flattens this into a single
/// stream of users, each annotated with a <c>role</c> field reflecting
/// which bucket it came from.
/// </para>
/// </remarks>
public sealed class Communities
{
    private readonly Transport.Transport _transport;

    internal Communities(Transport.Transport transport)
    {
        _transport = transport;
    }

    // -------------------------------------------------------------- reads

    /// <summary>Viewer-scoped community info. <c>GET /community_info</c>. Requires engagement cookies.</summary>
    /// <param name="communityId">Numeric community identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetInfoAsync(string communityId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/community_info",
            parameters: new Dictionary<string, object?> { ["community_id"] = communityId },
            sendCookies: true,
            ct: ct);

    /// <summary>
    /// Tight wrapper around <see cref="GetInfoAsync"/> — just the
    /// membership-state fields. <c>GET /community_check_member</c>.
    /// Requires engagement cookies.
    /// </summary>
    /// <param name="communityId">Numeric community identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> CheckMemberAsync(string communityId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/community_check_member",
            parameters: new Dictionary<string, object?> { ["community_id"] = communityId },
            sendCookies: true,
            ct: ct);

    /// <summary>
    /// One page of community members. <c>GET /community_members</c>.
    /// </summary>
    /// <remarks>
    /// Pagination uses <c>next_cursor</c>, not <c>cursor_bottom</c>. The
    /// payload is <c>{ count, members_by_role: { Admin: [...], Member: [...] }, next_cursor }</c>.
    /// </remarks>
    /// <param name="communityId">Numeric community identifier.</param>
    /// <param name="cursor">Cursor returned by a previous page.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> GetMembersAsync(
        string communityId,
        string? cursor = null,
        CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Get,
            "/community_members",
            parameters: new Dictionary<string, object?>
            {
                ["community_id"] = communityId,
                ["cursor"] = cursor,
            },
            ct: ct);

    /// <summary>
    /// Iterate every community member, flattening <c>members_by_role</c>
    /// into a single stream. Each yielded user carries an extra
    /// <c>role</c> property reflecting which bucket it came from
    /// (e.g. <c>Admin</c>, <c>Member</c>).
    /// </summary>
    /// <param name="communityId">Numeric community identifier.</param>
    /// <param name="maxPages">Stop after this many pages, if set.</param>
    /// <param name="maxItems">Stop after this many members, if set.</param>
    /// <param name="ct">Cancellation token.</param>
    public async IAsyncEnumerable<JsonElement> IterateMembersAsync(
        string communityId,
        int? maxPages = null,
        int? maxItems = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int yielded = 0;
        await foreach (var page in AsyncPaginator.IteratePagesAsync(
            fetch: (cursor, token) => GetMembersAsync(communityId, cursor, token),
            cursorField: "next_cursor",
            maxPages: maxPages,
            ct: ct).ConfigureAwait(false))
        {
            if (page.ValueKind != JsonValueKind.Object) continue;
            if (!page.TryGetProperty("members_by_role", out var roles) || roles.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var roleProp in roles.EnumerateObject())
            {
                if (roleProp.Value.ValueKind != JsonValueKind.Array) continue;
                string role = roleProp.Name;
                foreach (var user in roleProp.Value.EnumerateArray())
                {
                    if (user.ValueKind != JsonValueKind.Object) continue;
                    yield return AnnotateWithRole(user, role);
                    yielded += 1;
                    if (maxItems is { } cap && yielded >= cap) yield break;
                }
            }
        }
    }

    // ------------------------------------------------------------- writes

    /// <summary>Join a community. <c>POST /community_join</c>. Idempotent. Requires engagement cookies.</summary>
    /// <remarks>
    /// For approval-gated communities the server returns HTTP 409, which
    /// the SDK surfaces as a <see cref="TwtApiException"/> — branch to
    /// <see cref="RequestJoinAsync"/> then.
    /// </remarks>
    /// <param name="communityId">Numeric community identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> JoinAsync(string communityId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/community_join",
            jsonBody: new Dictionary<string, object?> { ["community_id"] = communityId },
            sendCookies: true,
            ct: ct);

    /// <summary>Leave a community. <c>POST /community_leave</c>. Idempotent. Requires engagement cookies.</summary>
    /// <param name="communityId">Numeric community identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> LeaveAsync(string communityId, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/community_leave",
            jsonBody: new Dictionary<string, object?> { ["community_id"] = communityId },
            sendCookies: true,
            ct: ct);

    /// <summary>
    /// Submit a pending join request to an approval-gated community.
    /// <c>POST /community_request_join</c>. Requires engagement cookies.
    /// </summary>
    /// <param name="communityId">Numeric community identifier.</param>
    /// <param name="answer">Optional free-text answer to the community's join question.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> RequestJoinAsync(
        string communityId,
        string? answer = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["community_id"] = communityId };
        if (answer is not null) payload["answer"] = answer;
        return _transport.RequestAsync(
            HttpMethod.Post,
            "/community_request_join",
            jsonBody: payload,
            sendCookies: true,
            ct: ct);
    }

    /// <summary>Return a copy of <paramref name="user"/> with an extra <c>role</c> field set to <paramref name="role"/>.</summary>
    private static JsonElement AnnotateWithRole(JsonElement user, string role)
    {
        // System.Text.Json elements are read-only. Re-emit the object via Utf8JsonWriter
        // with the extra "role" field appended, then re-parse into a JsonElement.
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var prop in user.EnumerateObject())
                prop.WriteTo(writer);
            writer.WriteString("role", role);
            writer.WriteEndObject();
        }
        buffer.Position = 0;
        using var doc = JsonDocument.Parse(buffer);
        return doc.RootElement.Clone();
    }
}
