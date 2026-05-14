using Microsoft.Extensions.Logging;

namespace Twtapi;

/// <summary>
/// Construction options for <see cref="TwtApi"/>.
/// </summary>
/// <remarks>
/// Only <see cref="ApiKey"/> is required. Reasonable defaults are applied
/// to everything else. Inject <see cref="HttpClient"/> to share an outer
/// pool, plug in a custom handler in tests, or hand over a
/// <c>SocketsHttpHandler</c> tuned for high throughput; otherwise the SDK
/// builds and owns its own client.
/// </remarks>
public sealed record TwtApiOptions
{
    /// <summary>twtapi API key (looks like <c>tw_…</c>). Required.</summary>
    public required string ApiKey { get; init; }

    /// <summary>Override the base URL. Defaults to <c>https://api.twtapi.io</c>.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Optional outbound proxy forwarded as the <c>X-Proxy</c> header.</summary>
    public string? Proxy { get; init; }

    /// <summary>Per-request deadline. Defaults to 30 seconds when omitted.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Max retry attempts. Defaults to 2; set <c>0</c> to disable retries entirely.</summary>
    public int? Retries { get; init; }

    /// <summary>Initial <c>auth_token</c> for engagement endpoints.</summary>
    public string? AuthToken { get; init; }

    /// <summary>Initial <c>ct0</c> for engagement endpoints.</summary>
    public string? Ct0 { get; init; }

    /// <summary>Optional structured logger. <see langword="null"/> disables logging.</summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// Inject a pre-configured <see cref="System.Net.Http.HttpClient"/> (for
    /// tests, custom handlers, or sharing pools). The SDK does NOT dispose
    /// injected clients; ownership stays with the caller. When omitted the
    /// SDK creates and owns its own client.
    /// </summary>
    public HttpClient? HttpClient { get; init; }
}
