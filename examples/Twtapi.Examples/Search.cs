using Twtapi;

namespace Twtapi.Examples;

/// <summary>Stream the latest matches for a query, capped to 25.</summary>
public static class Search
{
    public static async Task RunAsync(string apiKey)
    {
        using var client = new TwtApi(new TwtApiOptions { ApiKey = apiKey });

        int count = 0;
        await foreach (var tweet in client.Search.IterateAsync(
            query: "from:elonmusk lang:en",
            product: SearchProduct.Latest,
            maxItems: 25))
        {
            count += 1;
            string text = tweet.GetProperty("text").GetString() ?? "";
            string preview = text.Length > 80 ? text[..80] + "…" : text;
            Console.WriteLine($"{count,2}. {preview}");
        }
    }
}
