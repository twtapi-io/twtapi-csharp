using System.Collections.Concurrent;
using Xunit;

namespace Twtapi.Tests;

public class PaginationTests
{
    [Fact]
    public async Task IterateFollowers_Walks_Pages_Until_Cursor_Empty()
    {
        // Two pages then an empty cursor.
        var queue = new ConcurrentQueue<object>(new object[]
        {
            new
            {
                count = 2,
                followers = new[]
                {
                    new { user_id = "1", screen_name = "alice" },
                    new { user_id = "2", screen_name = "bob" },
                },
                cursor_bottom = "page2",
            },
            new
            {
                count = 1,
                followers = new[] { new { user_id = "3", screen_name = "carol" } },
                cursor_bottom = "",
            },
        });

        var handler = new FakeHttpMessageHandler(_ =>
        {
            queue.TryDequeue(out var payload);
            return FakeHttpMessageHandler.JsonOk(payload!);
        });

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        var collected = new List<string>();
        await foreach (var follower in client.Users.IterateFollowersAsync("42"))
        {
            collected.Add(follower.GetProperty("screen_name").GetString()!);
        }

        Assert.Equal(new[] { "alice", "bob", "carol" }, collected);
        Assert.Equal(2, handler.Requests.Count);
        // Second request carries the cursor from page 1.
        Assert.Equal("page2", handler.Requests[1].Query["cursor"]);
    }

    [Fact]
    public async Task Iterate_Honors_MaxItems_Cap()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOk(new
            {
                count = 5,
                tweets = new[]
                {
                    new { tweet_id = "1" }, new { tweet_id = "2" }, new { tweet_id = "3" },
                    new { tweet_id = "4" }, new { tweet_id = "5" },
                },
                cursor_bottom = "more",
            }));

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        var collected = new List<string>();
        await foreach (var tweet in client.Tweets.IterateQuotesAsync("123", maxItems: 3))
        {
            collected.Add(tweet.GetProperty("tweet_id").GetString()!);
        }

        Assert.Equal(new[] { "1", "2", "3" }, collected);
        Assert.Single(handler.Requests); // stopped inside the first page
    }

    [Fact]
    public async Task IterateReplyIds_Yields_Strings()
    {
        var queue = new ConcurrentQueue<object>(new object[]
        {
            new { count = 2, reply_ids = new[] { "111", "222" }, cursor_bottom = "next" },
            new { count = 1, reply_ids = new[] { "333" }, cursor_bottom = "" },
        });

        var handler = new FakeHttpMessageHandler(_ =>
        {
            queue.TryDequeue(out var payload);
            return FakeHttpMessageHandler.JsonOk(payload!);
        });

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        var collected = new List<string>();
        await foreach (var id in client.Tweets.IterateReplyIdsAsync("999"))
            collected.Add(id);

        Assert.Equal(new[] { "111", "222", "333" }, collected);
    }

    [Fact]
    public async Task IterateMembers_Flattens_MembersByRole_And_Tags_Each_User()
    {
        var queue = new ConcurrentQueue<object>(new object[]
        {
            new
            {
                count = 3,
                members_by_role = new Dictionary<string, object[]>
                {
                    ["Admin"] = new object[] { new { user_id = "1", screen_name = "alice" } },
                    ["Member"] = new object[]
                    {
                        new { user_id = "2", screen_name = "bob" },
                        new { user_id = "3", screen_name = "carol" },
                    },
                },
                next_cursor = "p2",
            },
            new
            {
                count = 1,
                members_by_role = new Dictionary<string, object[]>
                {
                    ["Member"] = new object[] { new { user_id = "4", screen_name = "dave" } },
                },
                next_cursor = "",
            },
        });

        var handler = new FakeHttpMessageHandler(_ =>
        {
            queue.TryDequeue(out var payload);
            return FakeHttpMessageHandler.JsonOk(payload!);
        });

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = "tw_key",
            HttpClient = new HttpClient(handler),
        });

        var collected = new List<(string Name, string Role)>();
        await foreach (var user in client.Communities.IterateMembersAsync("c1"))
        {
            collected.Add((
                user.GetProperty("screen_name").GetString()!,
                user.GetProperty("role").GetString()!));
        }

        Assert.Equal(4, collected.Count);
        Assert.Contains(("alice", "Admin"), collected);
        Assert.Contains(("bob", "Member"), collected);
        Assert.Contains(("carol", "Member"), collected);
        Assert.Contains(("dave", "Member"), collected);

        // Pagination used `next_cursor`, not `cursor_bottom`.
        Assert.Equal("p2", handler.Requests[1].Query["cursor"]);
    }
}
