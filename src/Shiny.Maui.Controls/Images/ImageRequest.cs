namespace Shiny.Maui.Controls.Images;

/// <summary>
/// Everything <see cref="IImageService"/> needs to fetch one image.
/// </summary>
/// <param name="Uri">The absolute URI to fetch.</param>
public record ImageRequest(string Uri)
{
    /// <summary>
    /// Skip both cache tiers and go to the network. The result is still written back to the cache -
    /// this forces a refresh, it does not disable caching. See <see cref="CacheEnabled"/> for that.
    /// </summary>
    public bool BypassCache { get; init; }

    /// <summary>When false, nothing is read from or written to either cache tier.</summary>
    public bool CacheEnabled { get; init; } = true;

    /// <summary>
    /// Overrides <see cref="ImageOptions.DiskCacheDuration"/> for this one entry. A server-supplied
    /// <c>Cache-Control</c>/<c>Expires</c> still wins over both.
    /// </summary>
    public TimeSpan? CacheDuration { get; init; }

    /// <summary>
    /// Extra request headers. The built-in <see cref="HttpImageDownloader"/> applies these per
    /// request; a custom <see cref="IImageDownloader"/> is free to ignore them and use its own
    /// pre-configured <c>HttpClient</c> instead.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Set by the service when it already holds an expired copy with an entity tag: send it as
    /// <c>If-None-Match</c> and answer <see cref="ImageDownloadResult.NotModified"/> on a 304.
    /// </summary>
    /// <remarks>
    /// A custom <see cref="IImageDownloader"/> that ignores this still works - it just re-downloads
    /// the whole image where the built-in one would have got a few hundred bytes of headers.
    /// </remarks>
    public string? IfNoneMatch { get; init; }

    /// <summary>
    /// Set by the service alongside <see cref="IfNoneMatch"/>, from the expired copy's
    /// <c>Last-Modified</c>: send it as <c>If-Modified-Since</c>.
    /// </summary>
    public DateTimeOffset? IfModifiedSince { get; init; }
}
