using Twtapi;

namespace Twtapi.Examples;

/// <summary>
/// Upload an image from a public URL and post a tweet that attaches it.
/// </summary>
public static class UploadMediaAndTweet
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

        var upload = await client.Media.UploadAsync("https://placehold.co/600x400/png");
        string mediaId = upload.GetProperty("media_id").GetString()!;
        Console.WriteLine($"Uploaded media_id: {mediaId} ({upload.GetProperty("size").GetInt64()} bytes)");

        var tweet = await client.Tweets.CreateAsync(
            text: "test image from the .NET SDK",
            options: new CreateTweetOptions { MediaId = mediaId });

        Console.WriteLine($"Posted tweet_id: {tweet.GetProperty("tweet_id").GetString()}");
    }
}
