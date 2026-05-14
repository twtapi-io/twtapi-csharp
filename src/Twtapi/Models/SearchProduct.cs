using System.Text.Json.Serialization;

namespace Twtapi;

/// <summary>
/// The ranking surface used by <c>GET /search</c>.
/// </summary>
/// <remarks>
/// The enum members are serialized as PascalCase strings — those are the
/// exact values the server accepts.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SearchProduct
{
    /// <summary>Top-ranked results.</summary>
    Top,

    /// <summary>Reverse-chronological results. Deepest cursor walk.</summary>
    Latest,

    /// <summary>People-only results.</summary>
    People,

    /// <summary>Results that include a photo.</summary>
    Photos,

    /// <summary>Results that include a video.</summary>
    Videos,
}
