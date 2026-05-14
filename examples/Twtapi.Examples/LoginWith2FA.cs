using Twtapi;

namespace Twtapi.Examples;

/// <summary>Full login flow with 2FA / email-code handling.</summary>
public static class LoginWith2FA
{
    public static async Task RunAsync(string apiKey)
    {
        string username = Environment.GetEnvironmentVariable("TWTAPI_USERNAME")
            ?? throw new InvalidOperationException("Set TWTAPI_USERNAME");
        string password = Environment.GetEnvironmentVariable("TWTAPI_PASSWORD")
            ?? throw new InvalidOperationException("Set TWTAPI_PASSWORD");

        using var client = new TwtApi(new TwtApiOptions { ApiKey = apiKey });

        var result = await client.Auth.LoginAsync(username, password);

        while (true)
        {
            switch (result)
            {
                case LoginResult.Ok ok:
                    client.SetCookies(ok.AuthToken, ok.Ct0);
                    Console.WriteLine("Login OK — cookies stored on the client.");
                    return;

                case LoginResult.Challenge { Type: "two_factor" } twoFactor:
                    Console.Write("2FA code from your authenticator app: ");
                    var code = Console.ReadLine() ?? "";
                    result = await client.Auth.Submit2FAAsync(twoFactor.State, code.Trim());
                    break;

                case LoginResult.Challenge emailChallenge:
                    Console.Write($"Code sent by {emailChallenge.Type}: ");
                    var emailCode = Console.ReadLine() ?? "";
                    result = await client.Auth.SubmitEmailCodeAsync(emailChallenge.State, emailCode.Trim());
                    break;

                case LoginResult.Error err:
                    Console.Error.WriteLine($"Login failed: {err.Message}");
                    return;
            }
        }
    }
}
