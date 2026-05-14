using System.Net;
using System.Text;
using Xunit;

namespace Twtapi.Tests;

public class TransportTests
{
    [Fact]
    public async Task Sends_ApiKey_And_UserAgent_Headers()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new { user_id = "1", username = "alice" }));
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_alice_key_12345678",
            HttpClient = new HttpClient(handler),
        });

        await client.Users.GetAsync("alice");

        var req = Assert.Single(handler.Requests);
        Assert.Equal("/user", req.Path);
        Assert.Equal("tw_alice_key_12345678", req.Header("X-API-Key"));
        Assert.Contains("twtapi-csharp/", req.Header("User-Agent") ?? "");
        Assert.Equal("alice", req.Query["username"]);
    }

    [Fact]
    public async Task Sends_Cookies_Only_When_Requested()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new { status = "ok" }));
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "auth-1",
            Ct0 = "ct0-1",
            HttpClient = new HttpClient(handler),
        });

        // Read endpoint: no cookies.
        await client.Users.GetAsync("alice");
        Assert.Null(handler.Requests[^1].Header("X-Twitter-Auth-Token"));

        // Engagement endpoint: cookies attached.
        await client.Tweets.LikeAsync("123");
        Assert.Equal("auth-1", handler.Requests[^1].Header("X-Twitter-Auth-Token"));
        Assert.Equal("ct0-1", handler.Requests[^1].Header("X-Twitter-Ct0"));
    }

    [Fact]
    public async Task Sends_Proxy_Header_When_Configured()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new { }));
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            Proxy = "http://user:pass@proxy.local:8080",
            HttpClient = new HttpClient(handler),
        });

        await client.Users.GetAsync("alice");

        Assert.Equal("http://user:pass@proxy.local:8080", handler.Requests[0].Header("X-Proxy"));
    }

    [Fact]
    public async Task Captures_RateLimit_Headers()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(
                new { user_id = "1" },
                headers: new Dictionary<string, string>
                {
                    ["X-RateLimit-Remaining"] = "42",
                    ["X-RateLimit-Limit"] = "100",
                    ["X-RateLimit-Reset"] = "1700000000",
                }));
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        Assert.Null(client.LastRateLimit);
        await client.Users.GetAsync("alice");

        Assert.NotNull(client.LastRateLimit);
        Assert.Equal(42, client.LastRateLimit!.Remaining);
        Assert.Equal(100, client.LastRateLimit.Limit);
        Assert.Equal(1700000000L, client.LastRateLimit.Reset);
    }

    [Fact]
    public async Task Tolerates_Missing_RateLimit_Limit_And_Reset()
    {
        // Live server only sends Remaining consistently.
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(
                new { user_id = "1" },
                headers: new Dictionary<string, string>
                {
                    ["X-RateLimit-Remaining"] = "9",
                }));
        using var client = new TwtApi(new TwtApiOptions { ApiKey = "tw_key", HttpClient = new HttpClient(handler) });

        await client.Users.GetAsync("alice");

        Assert.Equal(9, client.LastRateLimit!.Remaining);
        Assert.Null(client.LastRateLimit.Limit);
        Assert.Null(client.LastRateLimit.Reset);
    }

    [Fact]
    public async Task Sends_Json_Body_On_Post()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new { status = "ok" }));
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a",
            Ct0 = "c",
            HttpClient = new HttpClient(handler),
        });

        await client.Tweets.CreateAsync("hello", new CreateTweetOptions { MediaIds = new[] { "1", "2" } });

        var req = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/tweet", req.Path);
        Assert.Contains("\"text\":\"hello\"", req.Body);
        Assert.Contains("\"media_ids\":[\"1\",\"2\"]", req.Body);
    }

    [Fact]
    public async Task Retries_On_500_For_Idempotent_Calls()
    {
        int calls = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            calls += 1;
            if (calls < 3)
                return FakeHttpMessageHandler.JsonStatus(HttpStatusCode.InternalServerError, new { error = "internal", message = "boom" });
            return FakeHttpMessageHandler.JsonOk(new { user_id = "1" });
        });
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            Retries = 3,
            HttpClient = new HttpClient(handler),
        });

        var result = await client.Users.GetAsync("alice");

        Assert.Equal(3, calls);
        Assert.Equal("1", result.GetProperty("user_id").GetString());
    }

    [Fact]
    public async Task Does_Not_Retry_Tweet_Post_On_5xx()
    {
        int calls = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            calls += 1;
            return FakeHttpMessageHandler.JsonStatus(HttpStatusCode.InternalServerError, new { error = "internal", message = "boom" });
        });
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a",
            Ct0 = "c",
            Retries = 3,
            HttpClient = new HttpClient(handler),
        });

        await Assert.ThrowsAsync<InternalException>(() => client.Tweets.CreateAsync("hi"));
        Assert.Equal(1, calls); // never retried — could double-post
    }

    [Fact]
    public async Task Does_Not_Retry_Comment_Post_On_5xx()
    {
        int calls = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            calls += 1;
            return FakeHttpMessageHandler.JsonStatus(HttpStatusCode.BadGateway, new { error = "upstream_unavailable", message = "bad" });
        });
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a",
            Ct0 = "c",
            Retries = 3,
            HttpClient = new HttpClient(handler),
        });

        await Assert.ThrowsAsync<UpstreamException>(() => client.Tweets.CommentAsync("1", "hi"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Does_Not_Retry_4xx()
    {
        int calls = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            calls += 1;
            return FakeHttpMessageHandler.JsonStatus(HttpStatusCode.NotFound, new { error = "not_found", message = "nope" });
        });
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            Retries = 3,
            HttpClient = new HttpClient(handler),
        });

        await Assert.ThrowsAsync<NotFoundException>(() => client.Users.GetAsync("missing"));
        Assert.Equal(1, calls);
    }
}
