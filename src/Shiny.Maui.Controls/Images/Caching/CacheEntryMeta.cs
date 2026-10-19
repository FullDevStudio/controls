using System.Text.Json.Serialization;

namespace Shiny.Maui.Controls.Images.Caching;

/// <summary>
/// The sidecar written next to every cached image. Kept separate from the image file so the bytes
/// stay byte-identical to what the server sent and can be handed to a platform decoder untouched.
/// </summary>
public class CacheEntryMeta
{
    /// <summary>The URI this entry came from. Stored for diagnostics and for per-URI eviction.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>When the bytes were fetched.</summary>
    public DateTimeOffset DownloadedUtc { get; set; }

    /// <summary>When the entry goes stale.</summary>
    public DateTimeOffset ExpiresUtc { get; set; }

    /// <summary>The server's entity tag, if it sent one.</summary>
    public string? ETag { get; set; }

    /// <summary>The server's <c>Last-Modified</c>, if it sent one.</summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>Whether the entry has gone stale at <paramref name="now"/> and must be revalidated before it is trusted.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => this.ExpiresUtc <= now;

    /// <summary>Whether a stale copy can be revalidated cheaply instead of downloaded again.</summary>
    [JsonIgnore]
    public bool CanRevalidate => !String.IsNullOrEmpty(this.ETag) || this.LastModified is not null;

    /// <summary>Size of the image file in bytes.</summary>
    public long ContentLength { get; set; }

    /// <summary>The response media type, if known.</summary>
    public string? ContentType { get; set; }

    /// <summary>Last read. This is what LRU trimming orders by.</summary>
    public DateTimeOffset LastAccessUtc { get; set; }
}


/// <summary>
/// Source-generated serialization for <see cref="CacheEntryMeta"/>.
/// </summary>
/// <remarks>
/// Source generation rather than the reflection-based serializer because this assembly is built with
/// <c>IsAotCompatible</c>. Reflection here would raise IL2026/IL3050 across the whole package and,
/// worse, would work in debug and silently fail to round-trip once trimmed.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CacheEntryMeta))]
internal partial class ImageCacheJsonContext : JsonSerializerContext;
