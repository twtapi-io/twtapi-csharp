using Twtapi;

namespace Twtapi.Examples;

/// <summary>
/// Post a top-level tweet from the cookie owner's account.
/// </summary>
/// <remarks>
/// Set TWTAPI_AUTH_TOKEN and TWTAPI_CT0 in the environment (both come
/// out of <c>POST /login/start</c> or your existing session). Never
/// commit a real cookie pair.
/// </remarks>
public static class PostATweet
{
    public static async Task RunAsync(string apiKey)
    {
        string authToken = Environment.GetEnvironmentVariable("TWTAPI_AUTH_TOKEN")
            ?? throw new InvalidOperationException("Set TWTAPI_AUTH_TOKEN");
        string ct0 = Environment.GetEnvironmentVariable("TWTAPI_CT0")
            ?? throw new InvalidOperationException("Set TWTAPI_CT0");

        using var client = new TwtApi(new TwtApiOptions
        {
            ApiKey = apiKey,
            AuthToken = authToken,
            Ct0 = ct0,
        });

        // Persist the rotated ct0 if it changes mid-flight.
        client.Ct0Rotated += (_, e) =>
            Console.WriteLine($"[ct0 rotated → {e.NewCt0[..Math.Min(8, e.NewCt0.Length)]}…]");

        try
        {
            var result = await client.Tweets.CreateAsync(
                "Hello from the official .NET SDK ⌨️");

            Console.WriteLine($"Posted tweet_id: {result.GetProperty("tweet_id").GetString()}");
        }
        catch (DuplicateTweetException)
        {
            Console.Error.WriteLine("Already posted the same text recently.");
        }
        catch (TweetTooLongException)
        {
            Console.Error.WriteLine("Text exceeded the per-tweet limit.");
        }
    }
}
