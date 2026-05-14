using System.Text.Json;

namespace Twtapi.Resources;

/// <summary>
/// Media upload — <c>POST /upload_media</c>.
/// </summary>
/// <remarks>
/// The server downloads the file from a public URL on your behalf and
/// returns a <c>media_id</c> you can attach via
/// <see cref="CreateTweetOptions.MediaId"/> /
/// <see cref="CreateTweetOptions.MediaIds"/> (or the matching fields on
/// <see cref="CommentOptions"/>). Limits: 16 MiB; supported formats jpg,
/// png, gif, webp, bmp, mp4, mov, webm. The returned <c>media_id</c>
/// expires within ~15 minutes if not consumed.
/// </remarks>
public sealed class Media
{
    private readonly Transport.Transport _transport;

    internal Media(Transport.Transport transport)
    {
        _transport = transport;
    }

    /// <summary>Upload media from a public URL. Requires engagement cookies.</summary>
    /// <param name="mediaUrl">Public <c>https://</c> URL to the image or video.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<JsonElement> UploadAsync(string mediaUrl, CancellationToken ct = default) =>
        _transport.RequestAsync(
            HttpMethod.Post,
            "/upload_media",
            jsonBody: new Dictionary<string, object?> { ["media_url"] = mediaUrl },
            sendCookies: true,
            ct: ct);
}
