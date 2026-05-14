using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Twtapi.Transport;

/// <summary>
/// HTTP transport for the Twtapi SDK.
/// </summary>
/// <remarks>
/// <para>
/// Owns header injection (<c>X-API-Key</c>, optional engagement cookies,
/// optional <c>X-Proxy</c>), JSON encode/decode, error mapping, automatic
/// <c>ct0</c> rotation, retry policy, rate-limit tracking, and optional
/// structured logging with secret masking.
/// </para>
/// <para>
/// Resources call <see cref="RequestAsync"/> and get back a parsed
/// <see cref="JsonElement"/> or a <see cref="TwtApiException"/>.
/// </para>
/// </remarks>
internal sealed class Transport
{
    /// <summary>Default base URL for the API.</summary>
    public const string DefaultBaseUrl = "https://api.twtapi.io";
    /// <summary>Default per-request timeout.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    /// <summary>Default <c>User-Agent</c> sent on every request.</summary>
    public const string DefaultUserAgent = "twtapi-csharp/0.1.0";

    private const string NewCt0Header = "X-Twitter-New-Ct0";
    private static readonly HashSet<int> RetryStatuses = [408, 429, 500, 502, 503];
    private static readonly HashSet<string> NonIdempotentPaths = ["/tweet", "/comment"];
    private const int RetryAfterCapSeconds = 60;
    private const double BackoffCapSeconds = 8.0;

    private readonly string _apiKey;
    private readonly Uri _baseUri;
    private readonly string? _proxy;
    private readonly TimeSpan _timeout;
    private readonly int _retries;
    private readonly CookieState _cookies;
    private readonly ILogger? _logger;
    private readonly string _userAgent;
    private readonly HttpClient _httpClient;
    private readonly Action<string>? _onCt0Rotated;
    private RateLimit? _lastRateLimit;

    /// <summary>The cookie state shared with the resources.</summary>
    public CookieState Cookies => _cookies;

    /// <summary>Resolved base URL (without trailing slash).</summary>
    public Uri BaseUri => _baseUri;

    /// <summary>Latest <c>X-RateLimit-*</c> snapshot, or <see langword="null"/> until the first response.</summary>
    public RateLimit? LastRateLimit => Volatile.Read(ref _lastRateLimit);

    public Transport(
        string apiKey,
        HttpClient httpClient,
        CookieState cookies,
        Action<string>? onCt0Rotated,
        string? baseUrl = null,
        string? proxy = null,
        TimeSpan? timeout = null,
        int? retries = null,
        ILogger? logger = null,
        string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey is required", nameof(apiKey));

        _apiKey = apiKey;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _onCt0Rotated = onCt0Rotated;
        _baseUri = new Uri((baseUrl ?? DefaultBaseUrl).TrimEnd('/'), UriKind.Absolute);
        _proxy = string.IsNullOrEmpty(proxy) ? null : proxy;
        _timeout = timeout ?? DefaultTimeout;
        _retries = Math.Max(0, retries ?? 2);
        _logger = logger;
        _userAgent = userAgent ?? DefaultUserAgent;
    }

    /// <summary>
    /// Issue one HTTP request and return the parsed JSON body, or throw a
    /// <see cref="TwtApiException"/> subclass when the server returns
    /// 4xx/5xx (or network errors after exhausting retries).
    /// </summary>
    /// <param name="method">HTTP method (<c>GET</c>, <c>POST</c>, …).</param>
    /// <param name="path">Path under the base URL (e.g. <c>/user</c>).</param>
    /// <param name="parameters">Query parameters; <see langword="null"/> values are skipped.</param>
    /// <param name="jsonBody">Body to serialize as JSON. <see langword="null"/> sends no body.</param>
    /// <param name="sendCookies">Attach <c>X-Twitter-Auth-Token</c> + <c>X-Twitter-Ct0</c> if true.</param>
    /// <param name="extraHeaders">Additional headers (e.g. for <c>/csrf_token</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<JsonElement> RequestAsync(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, object?>? parameters = null,
        object? jsonBody = null,
        bool sendCookies = false,
        IReadOnlyDictionary<string, string>? extraHeaders = null,
        CancellationToken ct = default)
    {
        Uri url = BuildUri(path, parameters);
        bool retryable = IsRetryable(method, path);

        int attempt = 0;
        while (true)
        {
            attempt += 1;

            using var request = new HttpRequestMessage(method, url);
            ApplyHeaders(request, sendCookies, extraHeaders);

            if (jsonBody is not null)
            {
                string json = JsonSerializer.Serialize(jsonBody, JsonHelpers.RequestOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_timeout);

            long startedAtTicks = Environment.TickCount64;
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
            {
                long durationMs = Environment.TickCount64 - startedAtTicks;
                LogFailed(method.Method, path, DescribeNetworkError(ex), durationMs);
                if (attempt > _retries)
                {
                    throw new NetworkException(DescribeNetworkError(ex), ex);
                }
                await Task.Delay(BackoffDelay(attempt), ct).ConfigureAwait(false);
                continue;
            }

            try
            {
                long durationMs = Environment.TickCount64 - startedAtTicks;
                CaptureCt0Rotation(response);
                CaptureRateLimit(response);
                int status = (int)response.StatusCode;
                LogCompleted(method.Method, path, status, durationMs);

                if (RetryStatuses.Contains(status) && retryable && attempt <= _retries)
                {
                    TimeSpan wait = await ComputeRetryDelayAsync(status, response, attempt, ct).ConfigureAwait(false);
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait, ct).ConfigureAwait(false);
                    continue;
                }

                return await HandleResponseAsync(response, ct).ConfigureAwait(false);
            }
            finally
            {
                response?.Dispose();
            }
        }
    }

    // ------------------------------------------------------------- internals

    private Uri BuildUri(string path, IReadOnlyDictionary<string, object?>? parameters)
    {
        Uri baseTarget = Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? new Uri(path)
            : new Uri(_baseUri, path.StartsWith('/') ? path : "/" + path);

        if (parameters is null || parameters.Count == 0)
            return baseTarget;

        var sb = new StringBuilder();
        bool first = true;
        foreach (var kv in parameters)
        {
            if (kv.Value is null) continue;
            sb.Append(first ? '?' : '&');
            first = false;
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(FormatQueryValue(kv.Value)));
        }
        if (sb.Length == 0) return baseTarget;

        var builder = new UriBuilder(baseTarget);
        if (string.IsNullOrEmpty(builder.Query))
            builder.Query = sb.ToString()[1..]; // strip leading '?'
        else
            builder.Query = builder.Query[1..] + "&" + sb.ToString()[1..];
        return builder.Uri;
    }

    private static string FormatQueryValue(object value) => value switch
    {
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private void ApplyHeaders(
        HttpRequestMessage request,
        bool sendCookies,
        IReadOnlyDictionary<string, string>? extraHeaders)
    {
        request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        request.Headers.UserAgent.ParseAdd(_userAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_proxy is not null)
            request.Headers.TryAddWithoutValidation("X-Proxy", _proxy);
        if (sendCookies)
        {
            var (authToken, ct0) = _cookies.Snapshot();
            if (!string.IsNullOrEmpty(authToken))
                request.Headers.TryAddWithoutValidation("X-Twitter-Auth-Token", authToken);
            if (!string.IsNullOrEmpty(ct0))
                request.Headers.TryAddWithoutValidation("X-Twitter-Ct0", ct0);
        }
        if (extraHeaders is not null)
        {
            foreach (var kv in extraHeaders)
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }
    }

    private void CaptureCt0Rotation(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(NewCt0Header, out var values)) return;
        string? newCt0 = values.FirstOrDefault();
        if (string.IsNullOrEmpty(newCt0)) return;
        string? rotated = _cookies.RotateCt0(newCt0);
        if (rotated is not null)
        {
            try { _onCt0Rotated?.Invoke(rotated); }
            catch { /* User callback must not break the SDK. */ }
        }
    }

    private void CaptureRateLimit(HttpResponseMessage response)
    {
        int? limit = TryReadIntHeader(response, "X-RateLimit-Limit");
        int? remaining = TryReadIntHeader(response, "X-RateLimit-Remaining");
        long? reset = TryReadLongHeader(response, "X-RateLimit-Reset");
        if (limit is null && remaining is null && reset is null) return;
        Volatile.Write(ref _lastRateLimit, new RateLimit(limit, remaining, reset));
    }

    private static int? TryReadIntHeader(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values)) return null;
        string? raw = values.FirstOrDefault();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static long? TryReadLongHeader(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values)) return null;
        string? raw = values.FirstOrDefault();
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static async Task<TimeSpan> ComputeRetryDelayAsync(
        int status,
        HttpResponseMessage response,
        int attempt,
        CancellationToken ct)
    {
        if (status == 429)
        {
            double seconds = 1.0;
            // Safe to consume the body here — on retry the response is discarded
            // before HandleResponseAsync runs.
            try
            {
                string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("retry_after", out var prop)
                    && prop.ValueKind == JsonValueKind.Number
                    && prop.TryGetDouble(out var d))
                {
                    seconds = d;
                }
                else if (response.Headers.RetryAfter?.Delta is { } delta)
                {
                    seconds = delta.TotalSeconds;
                }
            }
            catch
            {
                if (response.Headers.RetryAfter?.Delta is { } delta)
                    seconds = delta.TotalSeconds;
            }
            return TimeSpan.FromSeconds(Math.Min(seconds, RetryAfterCapSeconds));
        }

        // Exponential backoff for 408 / 5xx: 0.5, 1.0, 2.0, … capped.
        double backoff = Math.Min(0.5 * Math.Pow(2, attempt - 1), BackoffCapSeconds);
        return TimeSpan.FromSeconds(backoff);
    }

    private static TimeSpan BackoffDelay(int attempt)
    {
        double backoff = Math.Min(0.5 * Math.Pow(2, attempt - 1), BackoffCapSeconds);
        return TimeSpan.FromSeconds(backoff);
    }

    private static async Task<JsonElement> HandleResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        int status = (int)response.StatusCode;
        string text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        JsonElement? parsed = TryParseJson(text);

        if (status >= 200 && status < 300)
        {
            if (parsed is { } p && p.ValueKind == JsonValueKind.Object)
                return p.Clone();
            // Synthesize a wrapper object so callers always get a JsonElement they can index.
            using var doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }

        double? retryAfterHeader = response.Headers.RetryAfter?.Delta?.TotalSeconds;
        throw ExceptionFactory.FromResponse(
            status,
            parsed,
            text,
            retryAfterHeader);
    }

    private static JsonElement? TryParseJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Treat as opaque body — surface raw text via BodyText on the exception.
            return null;
        }
    }

    private static bool IsRetryable(HttpMethod method, string path)
    {
        if (method != HttpMethod.Post) return true;
        int q = path.IndexOf('?');
        string basePath = q < 0 ? path : path[..q];
        if (!basePath.StartsWith('/')) basePath = "/" + basePath;
        return !NonIdempotentPaths.Contains(basePath);
    }

    private static string DescribeNetworkError(Exception ex) =>
        ex is OperationCanceledException ? "request timed out" : $"{ex.GetType().Name}: {ex.Message}";

    private void LogCompleted(string method, string path, int status, long durationMs)
    {
        if (_logger is null || !_logger.IsEnabled(LogLevel.Information)) return;
        _logger.LogInformation(
            "twtapi request method={Method} path={Path} status={Status} duration_ms={Duration} api_key={ApiKey}",
            method, path, status, durationMs, Mask(_apiKey));
    }

    private void LogFailed(string method, string path, string reason, long durationMs)
    {
        if (_logger is null || !_logger.IsEnabled(LogLevel.Warning)) return;
        _logger.LogWarning(
            "twtapi request failed method={Method} path={Path} error={Error} duration_ms={Duration} api_key={ApiKey}",
            method, path, reason, durationMs, Mask(_apiKey));
    }

    private static string Mask(string secret)
    {
        if (string.IsNullOrEmpty(secret)) return string.Empty;
        return secret.Length <= 8 ? secret + "…" : secret[..8] + "…";
    }
}

/// <summary>JSON helpers shared by the transport and resources.</summary>
internal static class JsonHelpers
{
    /// <summary>Used when serializing request bodies.</summary>
    public static readonly JsonSerializerOptions RequestOptions = new()
    {
        // Property names are stamped explicitly via [JsonPropertyName] where they matter.
        // Default camelCase keeps casual anonymous bodies readable.
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
