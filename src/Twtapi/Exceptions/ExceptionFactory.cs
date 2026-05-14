using System.Globalization;
using System.Text.Json;

namespace Twtapi;

/// <summary>
/// Builds the right <see cref="TwtApiException"/> subclass from an HTTP
/// response. Internal — consumers don't construct exceptions directly.
/// </summary>
internal static class ExceptionFactory
{
    /// <summary>
    /// Map a response (status + parsed body) onto the matching exception class.
    /// </summary>
    /// <param name="status">HTTP status code.</param>
    /// <param name="body">Parsed JSON body. May be <see langword="null"/> when the body wasn't JSON.</param>
    /// <param name="bodyText">Raw body text for the <c>BodyText</c> property.</param>
    /// <param name="retryAfterHeader">Parsed <c>Retry-After</c> header (seconds) for 429 fallback.</param>
    public static TwtApiException FromResponse(
        int status,
        JsonElement? body,
        string? bodyText,
        double? retryAfterHeader)
    {
        var (reason, message) = ExtractReasonAndMessage(body, status);

        if (status == 429)
        {
            double? retryAfterSeconds = TryGetDouble(body, "retry_after") ?? retryAfterHeader;
            TimeSpan? retryAfter = retryAfterSeconds.HasValue
                ? TimeSpan.FromSeconds(retryAfterSeconds.Value)
                : null;

            RateLimitScope? scope = TryGetString(body, "scope") switch
            {
                "plan" => RateLimitScope.Plan,
                "account" => RateLimitScope.Account,
                _ => null,
            };

            return new RateLimitException(message, status, reason, body, bodyText, retryAfter, scope);
        }

        if (status == 422)
        {
            return reason switch
            {
                "duplicate_tweet" or "tweet_silently_dropped_likely_duplicate" =>
                    new DuplicateTweetException(message, status, reason, body, bodyText),
                "tweet_too_long" =>
                    new TweetTooLongException(message, status, reason, body, bodyText),
                _ =>
                    new ValidationException(message, status, reason, body, bodyText),
            };
        }

        return status switch
        {
            400 => new BadRequestException(message, status, reason, body, bodyText),
            401 => new AuthenticationException(message, status, reason, body, bodyText),
            402 => new BillingException(message, status, reason, body, bodyText),
            403 => new PermissionException(message, status, reason, body, bodyText),
            404 => new NotFoundException(message, status, reason, body, bodyText),
            408 => new RequestTimeoutException(message, status, reason, body, bodyText),
            500 => new InternalException(message, status, reason, body, bodyText),
            502 => new UpstreamException(message, status, reason, body, bodyText),
            503 => new ServiceUnavailableException(message, status, reason, body, bodyText),
            _ => new TwtApiException(message, status, reason, body, bodyText),
        };
    }

    private static (string? Reason, string Message) ExtractReasonAndMessage(JsonElement? body, int status)
    {
        string? reason = TryGetString(body, "error");
        string? rawMessage = TryGetString(body, "message");
        string message = !string.IsNullOrEmpty(rawMessage)
            ? rawMessage
            : reason is not null
                ? $"HTTP {status}: {reason}"
                : $"HTTP {status}";
        return (reason, message);
    }

    private static string? TryGetString(JsonElement? body, string field)
    {
        if (body is not { ValueKind: JsonValueKind.Object } obj) return null;
        if (!obj.TryGetProperty(field, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static double? TryGetDouble(JsonElement? body, string field)
    {
        if (body is not { ValueKind: JsonValueKind.Object } obj) return null;
        if (!obj.TryGetProperty(field, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var s) => s,
            _ => null,
        };
    }
}
