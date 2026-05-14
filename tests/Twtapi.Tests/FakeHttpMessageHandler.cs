using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Twtapi.Tests;

/// <summary>
/// In-memory <see cref="HttpMessageHandler"/> for SDK tests.
/// </summary>
/// <remarks>
/// Records every outgoing request and replays canned responses. Inject
/// via <c>new TwtApiOptions { HttpClient = new HttpClient(handler) }</c>.
/// Keeping the test substrate hand-rolled lets us run without WireMock
/// or other interception libraries — matches the Python / TS SDKs.
/// </remarks>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<RecordedRequest, Task<HttpResponseMessage>> _responder;

    /// <summary>Every request the handler has seen, in order.</summary>
    public List<RecordedRequest> Requests { get; } = new();

    public FakeHttpMessageHandler(Func<RecordedRequest, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    /// <summary>Construct from a synchronous responder.</summary>
    public FakeHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> responder)
        : this(r => Task.FromResult(responder(r))) { }

    /// <summary>Construct from a queue: each call pops the next response.</summary>
    public static FakeHttpMessageHandler FromQueue(IEnumerable<HttpResponseMessage> responses)
    {
        var queue = new ConcurrentQueue<HttpResponseMessage>(responses);
        return new FakeHttpMessageHandler(_ =>
        {
            if (!queue.TryDequeue(out var response))
                throw new InvalidOperationException("FakeHttpMessageHandler queue exhausted.");
            return response;
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string? body = null;
        if (request.Content is not null)
            body = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        var record = new RecordedRequest(
            Method: request.Method,
            Uri: request.RequestUri!,
            Headers: request.Headers
                .SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v)))
                .ToList(),
            Body: body);
        Requests.Add(record);

        return await _responder(record).ConfigureAwait(false);
    }

    /// <summary>Build a <c>200 OK</c> response with a JSON body.</summary>
    public static HttpResponseMessage JsonOk(object payload, IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        if (headers is not null)
        {
            foreach (var kv in headers)
                response.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }
        return response;
    }

    /// <summary>Build a JSON response with an arbitrary status code.</summary>
    public static HttpResponseMessage JsonStatus(HttpStatusCode status, object payload, IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        if (headers is not null)
        {
            foreach (var kv in headers)
                response.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }
        return response;
    }
}

/// <summary>One captured outgoing request.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string? Body)
{
    public string Path => Uri.AbsolutePath;

    public string? Header(string name) =>
        Headers.FirstOrDefault(kv => kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    public IReadOnlyDictionary<string, string> Query
    {
        get
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(Uri.Query)) return dict;
            string query = Uri.Query.StartsWith('?') ? Uri.Query[1..] : Uri.Query;
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq < 0) { dict[System.Net.WebUtility.UrlDecode(pair)] = ""; continue; }
                dict[System.Net.WebUtility.UrlDecode(pair[..eq])] = System.Net.WebUtility.UrlDecode(pair[(eq + 1)..]);
            }
            return dict;
        }
    }
}
