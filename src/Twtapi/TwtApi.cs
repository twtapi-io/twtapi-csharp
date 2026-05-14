using Twtapi.Resources;

namespace Twtapi;

/// <summary>
/// Official .NET client for the twtapi.io HTTP API.
/// </summary>
/// <remarks>
/// <para>
/// Compose this once per application (or per tenant) and reuse it.
/// Resource namespaces (<see cref="Users"/>, <see cref="Tweets"/>,
/// <see cref="Search"/>, <see cref="Auth"/>, <see cref="Media"/>,
/// <see cref="Account"/>, <see cref="Communities"/>) hang off the
/// instance.
/// </para>
/// <para>
/// All numeric identifiers (<c>user_id</c>, <c>tweet_id</c>,
/// <c>community_id</c>, <c>media_id</c>) flow through the SDK as
/// <see cref="string"/> values to preserve full 64-bit precision and
/// match the wire format.
/// </para>
/// <example>
/// <code>
/// using var client = new TwtApi(new TwtApiOptions { ApiKey = "tw_..." });
/// JsonElement user = await client.Users.GetAsync("elonmusk");
///
/// // For engagement endpoints, supply X cookies once. The SDK auto-rotates
/// // ct0 whenever the server returns X-Twitter-New-Ct0.
/// client.SetCookies(authToken: "...", ct0: "...");
/// client.Ct0Rotated += (sender, e) =&gt; PersistCt0(e.NewCt0);
/// await client.Tweets.LikeAsync("1812256370960879853");
/// </code>
/// </example>
/// </remarks>
public sealed class TwtApi : IDisposable, IAsyncDisposable
{
    private readonly Transport.Transport _transport;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>User lookups, followers, tweet timeline, and follow action.</summary>
    public Users Users { get; }

    /// <summary>Tweet reads (retweets, quotes, comments, reply IDs) and engagement mutations.</summary>
    public Tweets Tweets { get; }

    /// <summary>Search tweets — <c>GET /search</c>.</summary>
    public Search Search { get; }

    /// <summary>Login flow, CSRF refresh, and whoami helper.</summary>
    public Auth Auth { get; }

    /// <summary>Media upload — <c>POST /upload_media</c>.</summary>
    public Media Media { get; }

    /// <summary>Account-level mutations (currently: password change).</summary>
    public Account Account { get; }

    /// <summary>Community lookups, membership checks, and join / leave actions.</summary>
    public Communities Communities { get; }

    /// <summary>Held engagement cookies (read <see cref="CookieState.Ct0"/> to persist after rotation).</summary>
    public CookieState Cookies { get; }

    /// <summary>Snapshot of the most recent <c>X-RateLimit-*</c> headers.</summary>
    public RateLimit? LastRateLimit => _transport.LastRateLimit;

    /// <summary>
    /// Fires after the SDK observes a fresh <c>ct0</c> in an
    /// <c>X-Twitter-New-Ct0</c> response header and stores it in
    /// <see cref="Cookies"/>. Subscribe to persist the new value.
    /// </summary>
    public event EventHandler<Ct0RotatedEventArgs>? Ct0Rotated;

    /// <summary>Construct a client.</summary>
    /// <param name="options">Construction options. <see cref="TwtApiOptions.ApiKey"/> is required.</param>
    public TwtApi(TwtApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Cookies = new CookieState(options.AuthToken, options.Ct0);

        if (options.HttpClient is not null)
        {
            _httpClient = options.HttpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
        }

        _transport = new Transport.Transport(
            apiKey: options.ApiKey,
            httpClient: _httpClient,
            cookies: Cookies,
            onCt0Rotated: RaiseCt0Rotated,
            baseUrl: options.BaseUrl,
            proxy: options.Proxy,
            timeout: options.Timeout,
            retries: options.Retries,
            logger: options.Logger);

        Users = new Users(_transport);
        Tweets = new Tweets(_transport);
        Search = new Search(_transport);
        Auth = new Auth(_transport);
        Media = new Media(_transport);
        Account = new Account(_transport, Cookies);
        Communities = new Communities(_transport);
    }

    /// <summary>Replace the held engagement cookies.</summary>
    /// <param name="authToken">New <c>auth_token</c>.</param>
    /// <param name="ct0">New <c>ct0</c>.</param>
    public void SetCookies(string authToken, string ct0)
    {
        Cookies.Set(authToken, ct0);
    }

    private void RaiseCt0Rotated(string newCt0)
    {
        var handler = Ct0Rotated;
        if (handler is null) return;
        try { handler(this, new Ct0RotatedEventArgs(newCt0)); }
        catch { /* user handler must not break the SDK */ }
    }

    /// <summary>Release the internally-owned <see cref="HttpClient"/>, if any.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
