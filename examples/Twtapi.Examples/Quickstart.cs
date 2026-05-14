using Twtapi;

namespace Twtapi.Examples;

/// <summary>Resolve a handle to a profile and print a couple of fields.</summary>
public static class Quickstart
{
    public static async Task RunAsync(string apiKey)
    {
        using var client = new TwtApi(new TwtApiOptions { ApiKey = apiKey });

        var user = await client.Users.GetAsync("elonmusk");

        // Responses are JsonElement — index by the field names the server returns.
        Console.WriteLine($"user_id:     {user.GetProperty("user_id").GetString()}");
        Console.WriteLine($"screen_name: {user.GetProperty("screen_name").GetString()}");
        Console.WriteLine($"name:        {user.GetProperty("name").GetString()}");
        Console.WriteLine($"followers:   {user.GetProperty("followers_count").GetInt64():N0}");
        Console.WriteLine($"following:   {user.GetProperty("friends_count").GetInt64():N0}");
        Console.WriteLine($"tweets:      {user.GetProperty("statuses_count").GetInt64():N0}");
    }
}
