namespace Twtapi;

/// <summary>
/// Optional parameters for <see cref="Resources.Tweets.CommentAsync"/>.
/// </summary>
public sealed record CommentOptions
{
    /// <summary>Single media identifier returned by <c>POST /upload_media</c>.</summary>
    public string? MediaId { get; init; }

    /// <summary>Up to four media identifiers returned by <c>POST /upload_media</c>.</summary>
    public IReadOnlyList<string>? MediaIds { get; init; }
}
