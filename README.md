# Twtapi — Official .NET client for [twtapi.io](https://twtapi.io)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/twtapi-io/twtapi-csharp?display_name=tag)](https://github.com/twtapi-io/twtapi-csharp/releases)

`Twtapi` is the official .NET client SDK for the
[twtapi.io](https://twtapi.io) HTTP API — a JSON API that exposes 𝕏
(Twitter) data and actions: read profiles and timelines, search tweets,
log in to an account, post / like / retweet / follow, manage communities,
and more.

- **Target framework**: `.NET 8` (LTS).
- **Runtime dependencies**: only
  [`Microsoft.Extensions.Logging.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions).
  JSON via `System.Text.Json`, HTTP via the built-in `HttpClient`.
- **Companion SDKs**: [Python](https://pypi.org/project/twtapi/) ·
  [TypeScript / Node.js](https://www.npmjs.com/package/@twtapi-io/twtapi)

## Install

Grab the binaries from the latest
[GitHub Release](https://github.com/twtapi-io/twtapi-csharp/releases/latest).

**Option 1 — reference the DLL directly.** Download `Twtapi.dll` and
`Twtapi.xml` (the XML next to the DLL gives you IntelliSense), drop
both into a `lib/` folder in your project, and add a reference:

```xml
<ItemGroup>
  <Reference Include="Twtapi">
    <HintPath>lib\Twtapi.dll</HintPath>
  </Reference>
</ItemGroup>
```

**Option 2 — install the `.nupkg` from a local NuGet source.** Download
`Twtapi.<version>.nupkg`, place it in a folder (e.g. `C:\local-nuget\`),
register it as a source once:

```bash
dotnet nuget add source C:\local-nuget --name local
```

Then in your project:

```bash
dotnet add package Twtapi
```

## Quickstart

```csharp
using Twtapi;

using var client = new TwtApi(new TwtApiOptions
{
    ApiKey = "tw_...",
});

// Resolve a handle to a user object. Responses are JsonElement values
// — index by the field names the server actually returns.
var user = await client.Users.GetAsync("elonmusk");
Console.WriteLine(user.GetProperty("user_id").GetString());
Console.WriteLine(user.GetProperty("screen_name").GetString());
Console.WriteLine(user.GetProperty("followers_count").GetInt64());
```

Get an API key at <https://twtapi.io/dashboard>.

## Engagement endpoints

Endpoints that act on an 𝕏 account (post a tweet, like, follow, …)
require an `auth_token` + `ct0` cookie pair from that account. Set them
once, then call any engagement method:

```csharp
client.SetCookies(authToken: "...", ct0: "...");

// Persist the rotated ct0 — the server may rotate it mid-flight via the
// X-Twitter-New-Ct0 header, and the SDK auto-updates Cookies.Ct0.
client.Ct0Rotated += (sender, e) => SaveCt0(e.NewCt0);

await client.Tweets.LikeAsync("1812256370960879853");
await client.Tweets.CreateAsync("Hello from the .NET SDK!");
```

### Login flow

The login endpoints exchange username + password for an `auth_token` +
`ct0` pair you can then use for engagement. Pattern-match on the
discriminated `LoginResult`:

```csharp
var result = await client.Auth.LoginAsync("yourhandle", "yourpassword");
switch (result)
{
    case LoginResult.Ok ok:
        client.SetCookies(ok.AuthToken, ok.Ct0);
        break;

    case LoginResult.Challenge challenge when challenge.Type == "two_factor":
        var code = ReadFromAuthenticatorApp();
        var next = await client.Auth.Submit2FAAsync(challenge.State, code);
        // Recurse on `next` (Ok / Challenge / Error).
        break;

    case LoginResult.Challenge challenge:
        var emailCode = ReadFromEmail();
        await client.Auth.SubmitEmailCodeAsync(challenge.State, emailCode);
        break;

    case LoginResult.Error err:
        Console.Error.WriteLine(err.Message);
        break;
}
```

## Pagination

Every paginated endpoint has both a single-page method and an
`IAsyncEnumerable<T>` iterator:

```csharp
// One page at a time.
var page = await client.Users.GetFollowersAsync("44196397", count: 200);
foreach (var follower in page.GetProperty("followers").EnumerateArray())
    Console.WriteLine(follower.GetProperty("screen_name").GetString());

// Walk the whole list, capped to 1 000 items.
await foreach (var follower in client.Users.IterateFollowersAsync(
    userId: "44196397",
    count: 200,
    maxItems: 1_000))
{
    Console.WriteLine(follower.GetProperty("screen_name").GetString());
}
```

`/community_members` is special — it groups members by role under
`members_by_role` and uses `next_cursor` rather than `cursor_bottom`. The
SDK iterator flattens both into one stream and tags every user with a
`role` field:

```csharp
await foreach (var user in client.Communities.IterateMembersAsync("1493446837214187523"))
{
    string role = user.GetProperty("role").GetString()!;   // e.g. "Admin", "Member"
    string handle = user.GetProperty("screen_name").GetString()!;
    Console.WriteLine($"{role}: {handle}");
}
```

## Search

```csharp
await foreach (var tweet in client.Search.IterateAsync(
    query: "from:elonmusk lang:en",
    product: SearchProduct.Latest,
    maxItems: 500))
{
    Console.WriteLine(tweet.GetProperty("text").GetString());
}
```

## Media upload + tweet with media

```csharp
var upload = await client.Media.UploadAsync("https://example.com/cat.png");
string mediaId = upload.GetProperty("media_id").GetString()!;

await client.Tweets.CreateAsync(
    "look at this cat",
    new CreateTweetOptions { MediaId = mediaId });
```

`media_id` is bound to the cookie owner and expires within ~15 minutes —
upload and tweet in the same workflow.

## Errors

Every failure surfaces as a `TwtApiException` subclass. Catch the
specific one you care about; fall through to the base for everything
else:

```csharp
try
{
    await client.Tweets.CreateAsync("hi");
}
catch (RateLimitException ex)
{
    await Task.Delay(ex.RetryAfter ?? TimeSpan.FromSeconds(5));
}
catch (DuplicateTweetException)
{
    // Already posted recently — drop or de-dupe.
}
catch (TweetTooLongException)
{
    // Split or truncate.
}
catch (TwtApiException ex)
{
    Console.Error.WriteLine($"twtapi failed: {ex.Status} {ex.ErrorCode} — {ex.Message}");
}
```

| HTTP | Exception |
|---|---|
| 400 | `BadRequestException` |
| 401 | `AuthenticationException` |
| 402 | `BillingException` |
| 403 | `PermissionException` |
| 404 | `NotFoundException` |
| 408 | `RequestTimeoutException` |
| 422 | `ValidationException` (with `DuplicateTweetException`, `TweetTooLongException`) |
| 429 | `RateLimitException` (carries `RetryAfter`, `Scope`) |
| 500 | `InternalException` |
| 502 | `UpstreamException` |
| 503 | `ServiceUnavailableException` |
| network | `NetworkException` |

Every exception exposes `Status`, `ErrorCode` (the stable machine code),
`Message`, `Body` (parsed JSON), and `BodyText` (raw).

## Rate limits

```csharp
var snapshot = client.LastRateLimit;
Console.WriteLine($"{snapshot?.Remaining} requests left in the current window");
```

The live server reliably sends `X-RateLimit-Remaining`; `Limit` and
`Reset` are surfaced when present and `null` otherwise.

## Retries

By default the SDK retries safe failures up to 2 times:

- **429** — retried once after `retry_after` (cap 60 s).
- **408 / 500 / 502 / 503** — retried on idempotent endpoints with
  exponential backoff (cap ~8 s).
- **Network errors** — retried up to 2× with backoff.
- **4xx semantic** (400/401/402/403/404/422) — never retried.
- **`POST /tweet` and `POST /comment`** — never retried on 5xx (could
  double-post).

Pass `Retries = 0` in `TwtApiOptions` to disable retries entirely.

## Logging

The SDK accepts an optional `Microsoft.Extensions.Logging.ILogger`. When
set, it logs method, path, status, duration, and a masked API key
prefix. Request and response bodies are **never** logged.

```csharp
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

using var client = new TwtApi(new TwtApiOptions
{
    ApiKey = "tw_...",
    Logger = loggerFactory.CreateLogger<TwtApi>(),
});
```

## Identifiers as `string`

Every numeric identifier (`user_id`, `tweet_id`, `community_id`,
`media_id`) is a `string` on the wire and in the SDK surface. They're
typically 64-bit values that lose precision when round-tripped through
JavaScript and some JSON tools — keeping them as strings is the
defensive choice the entire twtapi SDK family makes.

## Custom `HttpClient`

For tests, custom handlers, DI, or shared connection pools, inject your
own `HttpClient`. The SDK never disposes an injected client.

```csharp
var custom = new HttpClient(new MyDelegatingHandler());
using var client = new TwtApi(new TwtApiOptions
{
    ApiKey = "tw_...",
    HttpClient = custom,
});
```

When omitted, the SDK creates and owns its own `HttpClient` and
disposes it from `Dispose()` / `DisposeAsync()`.

## Examples

See [`examples/Twtapi.Examples`](examples/Twtapi.Examples/) for runnable
demos:

```bash
export TWTAPI_KEY="tw_..."
dotnet run --project examples/Twtapi.Examples -- quickstart
dotnet run --project examples/Twtapi.Examples -- walk-followers
dotnet run --project examples/Twtapi.Examples -- search
dotnet run --project examples/Twtapi.Examples -- post-tweet
dotnet run --project examples/Twtapi.Examples -- upload-media
dotnet run --project examples/Twtapi.Examples -- login-2fa
```

## Development

```bash
git clone https://github.com/twtapi-io/twtapi-csharp
cd twtapi-csharp
dotnet restore
dotnet build -c Release
dotnet test
```

## License

[MIT](LICENSE) © twtapi.io
