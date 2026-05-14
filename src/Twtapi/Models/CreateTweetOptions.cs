namespace Twtapi;

/// <summary>
/// Optional parameters for <see cref="Resources.Tweets.CreateAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="InReplyTo"/> and <see cref="AttachmentUrl"/> are mutually
/// exclusive. To attach media, upload it first with
/// <see cref="Resources.Media.UploadAsync"/> and pass either a single
/// <see cref="MediaId"/> or up to four IDs in <see cref="MediaIds"/>.
/// </para>
/// <para>
/// All identifiers are <see cref="string"/> rather than <see cref="long"/>
/// to preserve full 64-bit precision and match the wire format.
/// </para>
/// </remarks>
public sealed record CreateTweetOptions
{
    /// <summary>Tweet ID to reply to. Omit for a top-level tweet.</summary>
    public string? InReplyTo { get; init; }

    /// <summary>URL of a tweet to quote. Mutually exclusive with <see cref="InReplyTo"/>.</summary>
    public string? AttachmentUrl { get; init; }

    /// <summary>Single media identifier returned by <c>POST /upload_media</c>.</summary>
    public string? MediaId { get; init; }

    /// <summary>Up to four media identifiers returned by <c>POST /upload_media</c>.</summary>
    public IReadOnlyList<string>? MediaIds { get; init; }
}
