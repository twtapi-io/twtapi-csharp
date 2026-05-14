using System.Text.Json;

namespace Twtapi;

/// <summary>
/// Base class for every exception raised by the Twtapi SDK.
/// </summary>
/// <remarks>
/// <para>
/// Carries the HTTP <see cref="Status"/>, the server's machine-readable
/// <see cref="ErrorCode"/> (the <c>error</c> field of the JSON body),
/// the human-readable <see cref="Exception.Message"/>, and the raw
/// response <see cref="Body"/>.
/// </para>
/// <para>
/// Catch this base class to handle any SDK failure; catch a concrete
/// subclass (<see cref="RateLimitException"/>, <see cref="NotFoundException"/>,
/// etc.) to react to specific cases.
/// </para>
/// </remarks>
public class TwtApiException : Exception
{
    /// <summary>HTTP status code. Null for transport-level network failures.</summary>
    public int? Status { get; }

    /// <summary>
    /// Stable machine-readable error code from the JSON body's <c>error</c>
    /// field. Match on this, not on <see cref="Exception.Message"/>.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Parsed JSON response body, or <see langword="null"/> if the body
    /// could not be parsed as JSON. Borrowed reference owned by the
    /// exception.
    /// </summary>
    public JsonElement? Body { get; }

    /// <summary>Raw response body text (best-effort, may be truncated).</summary>
    public string? BodyText { get; }

    /// <summary>Internal constructor — only the SDK builds these.</summary>
    /// <param name="message">Human-readable message.</param>
    /// <param name="status">HTTP status code.</param>
    /// <param name="errorCode">The <c>error</c> field of the JSON body.</param>
    /// <param name="body">Parsed JSON body.</param>
    /// <param name="bodyText">Raw body text.</param>
    /// <param name="innerException">Underlying transport exception, if any.</param>
    internal TwtApiException(
        string message,
        int? status = null,
        string? errorCode = null,
        JsonElement? body = null,
        string? bodyText = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Status = status;
        ErrorCode = errorCode;
        Body = body;
        BodyText = bodyText;
    }
}
