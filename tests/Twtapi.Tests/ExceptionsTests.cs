using System.Net;
using Xunit;

namespace Twtapi.Tests;

public class ExceptionsTests
{
    private static TwtApi BuildClient(HttpStatusCode status, object body) =>
        new(new TwtApiOptions
        {
            ApiKey = "tw_key",
            Retries = 0,
            AuthToken = "a",
            Ct0 = "c",
            HttpClient = new HttpClient(new FakeHttpMessageHandler(_ =>
                FakeHttpMessageHandler.JsonStatus(status, body))),
        });

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(BadRequestException))]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.PaymentRequired, typeof(BillingException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(PermissionException))]
    [InlineData(HttpStatusCode.NotFound, typeof(NotFoundException))]
    [InlineData(HttpStatusCode.RequestTimeout, typeof(RequestTimeoutException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(InternalException))]
    [InlineData(HttpStatusCode.BadGateway, typeof(UpstreamException))]
    [InlineData(HttpStatusCode.ServiceUnavailable, typeof(ServiceUnavailableException))]
    public async Task Status_Maps_To_Specific_Exception(HttpStatusCode status, Type expected)
    {
        using var client = BuildClient(status, new { error = "code", message = "msg" });
        var ex = await Assert.ThrowsAnyAsync<TwtApiException>(() => client.Users.GetAsync("alice"));
        Assert.IsType(expected, ex);
        Assert.Equal((int)status, ex.Status);
        Assert.Equal("code", ex.ErrorCode);
        Assert.Equal("msg", ex.Message);
    }

    [Fact]
    public async Task Status_422_Generic_Maps_To_ValidationException()
    {
        using var client = BuildClient(HttpStatusCode.UnprocessableEntity, new { error = "other_reason", message = "..." });
        var ex = await Assert.ThrowsAsync<ValidationException>(() => client.Users.GetAsync("alice"));
        Assert.Equal(422, ex.Status);
    }

    [Fact]
    public async Task Status_422_DuplicateTweet_Surfaces_DuplicateTweetException()
    {
        using var client = BuildClient(HttpStatusCode.UnprocessableEntity, new { error = "duplicate_tweet", message = "dup" });
        var ex = await Assert.ThrowsAsync<DuplicateTweetException>(() => client.Tweets.CreateAsync("hi"));
        Assert.IsAssignableFrom<ValidationException>(ex);
        Assert.Equal("duplicate_tweet", ex.ErrorCode);
    }

    [Fact]
    public async Task Status_422_SilentDuplicate_Surfaces_DuplicateTweetException()
    {
        using var client = BuildClient(HttpStatusCode.UnprocessableEntity, new
        {
            error = "tweet_silently_dropped_likely_duplicate",
            message = "stealth dedupe",
        });
        var ex = await Assert.ThrowsAsync<DuplicateTweetException>(() => client.Tweets.CreateAsync("hi"));
        Assert.Equal("tweet_silently_dropped_likely_duplicate", ex.ErrorCode);
    }

    [Fact]
    public async Task Status_422_TooLong_Surfaces_TweetTooLongException()
    {
        using var client = BuildClient(HttpStatusCode.UnprocessableEntity, new { error = "tweet_too_long", message = "tldr" });
        var ex = await Assert.ThrowsAsync<TweetTooLongException>(() => client.Tweets.CreateAsync(new string('x', 9000)));
        Assert.IsAssignableFrom<ValidationException>(ex);
    }

    [Fact]
    public async Task Status_429_Surfaces_RateLimit_With_Metadata()
    {
        using var client = BuildClient(
            (HttpStatusCode)429,
            new { error = "rate_limited", message = "slow down", retry_after = 12, scope = "plan" });

        var ex = await Assert.ThrowsAsync<RateLimitException>(() => client.Users.GetAsync("alice"));
        Assert.Equal(429, ex.Status);
        Assert.Equal(TimeSpan.FromSeconds(12), ex.RetryAfter);
        Assert.Equal(RateLimitScope.Plan, ex.Scope);
    }

    [Fact]
    public async Task Status_429_Scope_Account()
    {
        using var client = BuildClient(
            (HttpStatusCode)429,
            new { error = "rate_limited", message = "...", retry_after = 5, scope = "account" });

        var ex = await Assert.ThrowsAsync<RateLimitException>(() => client.Users.GetAsync("alice"));
        Assert.Equal(RateLimitScope.Account, ex.Scope);
    }
}
