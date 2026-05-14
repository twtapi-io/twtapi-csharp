using Xunit;

namespace Twtapi.Tests;

public class CookieStateTests
{
    [Fact]
    public async Task Captures_X_Twitter_New_Ct0_And_Updates_Cookies()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(
                new { status = "ok" },
                headers: new Dictionary<string, string>
                {
                    ["X-Twitter-New-Ct0"] = "rotated-ct0",
                }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a",
            Ct0 = "original-ct0",
            HttpClient = new HttpClient(handler),
        });

        Assert.Equal("original-ct0", client.Cookies.Ct0);

        string? rotated = null;
        client.Ct0Rotated += (_, e) => rotated = e.NewCt0;

        await client.Tweets.LikeAsync("123");

        Assert.Equal("rotated-ct0", client.Cookies.Ct0);
        Assert.Equal("rotated-ct0", rotated);
    }

    [Fact]
    public async Task Ct0Rotation_Event_Does_Not_Fire_For_Unchanged_Value()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(
                new { status = "ok" },
                headers: new Dictionary<string, string>
                {
                    ["X-Twitter-New-Ct0"] = "same-ct0",
                }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a",
            Ct0 = "same-ct0",
            HttpClient = new HttpClient(handler),
        });

        int firings = 0;
        client.Ct0Rotated += (_, _) => firings += 1;

        await client.Tweets.LikeAsync("123");

        Assert.Equal(0, firings);
        Assert.Equal("same-ct0", client.Cookies.Ct0);
    }

    [Fact]
    public async Task ChangePassword_Auto_Rotates_Held_Cookie_Pair()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new
            {
                status = "ok",
                password = "newpw",
                generated = false,
                new_auth_token = "new-auth",
                new_ct0 = "new-ct0",
            }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "old-auth",
            Ct0 = "old-ct0",
            HttpClient = new HttpClient(handler),
        });

        await client.Account.ChangePasswordAsync("OldPw1!", "NewPw1!");

        Assert.Equal("new-auth", client.Cookies.AuthToken);
        Assert.Equal("new-ct0", client.Cookies.Ct0);
    }

    [Fact]
    public void SetCookies_Replaces_Stored_Pair()
    {
        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            AuthToken = "a1",
            Ct0 = "c1",
            HttpClient = new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonOk(new { }))),
        });

        client.SetCookies("a2", "c2");

        Assert.Equal("a2", client.Cookies.AuthToken);
        Assert.Equal("c2", client.Cookies.Ct0);
    }

    [Fact]
    public async Task Login_State_Passing_Returns_Discriminated_Union()
    {
        // First response: challenge. Second: ok.
        var handler = FakeHttpMessageHandler.FromQueue(new[]
        {
            FakeHttpMessageHandler.JsonOk(new { status = "challenge", type = "two_factor", state = "state-xyz" }),
            FakeHttpMessageHandler.JsonOk(new { status = "ok", auth_token = "AT", ct0 = "CT" }),
        });

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        var first = await client.Auth.LoginAsync("user", "pw");
        var challenge = Assert.IsType<LoginResult.Challenge>(first);
        Assert.Equal("two_factor", challenge.Type);
        Assert.Equal("state-xyz", challenge.State);

        var second = await client.Auth.Submit2FAAsync(challenge.State, "123456");
        var ok = Assert.IsType<LoginResult.Ok>(second);
        Assert.Equal("AT", ok.AuthToken);
        Assert.Equal("CT", ok.Ct0);
    }
}
