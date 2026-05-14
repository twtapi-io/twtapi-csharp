using Xunit;

namespace Twtapi.Tests;

public class ResourcesTests
{
    [Fact]
    public async Task Media_Upload_Sends_MediaUrl_In_Body_With_Cookies()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new
            {
                status = "ok",
                media_id = "9999",
                size = 1024,
                media_type = "image/png",
            }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a",
            Ct0 = "c",
            HttpClient = new HttpClient(handler),
        });

        var result = await client.Media.UploadAsync("https://example.com/image.png");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/upload_media", req.Path);
        Assert.Equal("a", req.Header("X-Twitter-Auth-Token"));
        Assert.Equal("c", req.Header("X-Twitter-Ct0"));
        Assert.Contains("\"media_url\":\"https://example.com/image.png\"", req.Body);
        Assert.Equal("9999", result.GetProperty("media_id").GetString());
    }

    [Fact]
    public async Task Auth_CsrfToken_Sends_Only_AuthToken_Header()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new { status = "ok", ct0 = "fresh" }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        await client.Auth.CsrfTokenAsync("AUTH_TOKEN_X");

        var req = Assert.Single(handler.Requests);
        Assert.Equal("AUTH_TOKEN_X", req.Header("X-Twitter-Auth-Token"));
        Assert.Null(req.Header("X-Twitter-Ct0"));
    }

    [Fact]
    public async Task Search_Serializes_Product_As_PascalCase_String()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new { tweets = Array.Empty<object>(), cursor_bottom = "" }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        await client.Search.QueryAsync("hello", product: SearchProduct.Latest, count: 50);

        var req = handler.Requests[0];
        Assert.Equal("hello", req.Query["q"]);
        Assert.Equal("Latest", req.Query["product"]);
        Assert.Equal("50", req.Query["count"]);
    }

    [Fact]
    public async Task CreateTweet_With_SingleMediaId_Uses_media_id_Field()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new { status = "ok", tweet_id = "1" }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a",
            Ct0 = "c",
            HttpClient = new HttpClient(handler),
        });

        await client.Tweets.CreateAsync("with media", new CreateTweetOptions { MediaId = "abc" });

        Assert.Contains("\"media_id\":\"abc\"", handler.Requests[0].Body);
        Assert.DoesNotContain("\"media_ids\"", handler.Requests[0].Body);
    }
}
