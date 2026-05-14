using Twtapi;

namespace Twtapi.Examples;

/// <summary>Walk the followers of a public account, capped to 100.</summary>
public static class WalkFollowers
{
    public static async Task RunAsync(string apiKey)
    {
        using var client = new TwtApi(new TwtApiOptions { ApiKey = apiKey });

        // Resolve the handle to a user_id first.
        var elonId = (await client.Users.GetByUsernameAsync("elonmusk"))
            .GetProperty("user_id").GetString()!;

        int count = 0;
        await foreach (var follower in client.Users.IterateFollowersAsync(elonId, count: 200, maxItems: 100))
        {
            count += 1;
            Console.WriteLine($"{count,3}. {follower.GetProperty("screen_name").GetString()}");
        }

        Console.WriteLine($"\nLast page snapshot: {client.LastRateLimit?.Remaining} requests left.");
    }
}
