using Twtapi.Examples;

string command = args.Length > 0 ? args[0] : "quickstart";

string apiKey = Environment.GetEnvironmentVariable("TWTAPI_KEY")
    ?? throw new InvalidOperationException("Set TWTAPI_KEY before running an example.");

switch (command)
{
    case "quickstart":
        await Quickstart.RunAsync(apiKey);
        break;
    case "walk-followers":
        await WalkFollowers.RunAsync(apiKey);
        break;
    case "search":
        await Search.RunAsync(apiKey);
        break;
    case "post-tweet":
        await PostATweet.RunAsync(apiKey);
        break;
    case "upload-media":
        await UploadMediaAndTweet.RunAsync(apiKey);
        break;
    case "login-2fa":
        await LoginWith2FA.RunAsync(apiKey);
        break;
    default:
        Console.WriteLine("Unknown example. Available:");
        Console.WriteLine("  quickstart  walk-followers  search  post-tweet  upload-media  login-2fa");
        break;
}
