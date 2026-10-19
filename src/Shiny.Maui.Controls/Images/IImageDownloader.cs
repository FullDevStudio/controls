namespace Shiny.Maui.Controls.Images;

/// <summary>
/// What a download produced. The <see cref="Stream"/> is handed over unread so the caller can pump
/// it and report progress; the caller owns disposing it.
/// </summary>
/// <param name="Stream">The response body, positioned at the start.</param>
/// <param name="ContentLength">
/// The expected size when the server sent one. This single value decides whether the user sees a
/// filling ring or a spinning one, which is why it is surfaced separately rather than left for the
/// caller to dig out of headers it cannot see.
/// </param>
/// <param name="ContentType">The response media type, if any.</param>
/// <param name="ExpiresUtc">
/// When the entry should be considered stale, derived from <c>Cache-Control: max-age</c> or
/// <c>Expires</c>. Null hands the decision back to <see cref="ImageOptions.DiskCacheDuration"/>.
/// </param>
/// <param name="ETag">The entity tag, stored for future revalidation.</param>
/// <param name="LastModified">The <c>Last-Modified</c> header, stored for future revalidation.</param>
/// <param name="NotModified">
/// The server answered a conditional request with 304: the cached copy is still good, and
/// <paramref name="Stream"/> is empty. Only meaningful when the request carried
/// <see cref="ImageRequest.IfNoneMatch"/> or <see cref="ImageRequest.IfModifiedSince"/>.
/// </param>
public record ImageDownloadResult(
    Stream Stream,
    long? ContentLength = null,
    string? ContentType = null,
    DateTimeOffset? ExpiresUtc = null,
    string? ETag = null,
    DateTimeOffset? LastModified = null,
    bool NotModified = false
);


/// <summary>
/// How image bytes are fetched. Replace this - not the whole <see cref="IImageService"/> - when all
/// you need is your own <c>HttpClient</c>: auth headers, cookies, a custom handler, certificate
/// pinning. Caching, queueing and de-duplication stay where they are.
/// </summary>
/// <example>
/// <code>
/// class AuthenticatedDownloader(HttpClient client, ITokenStore tokens) : IImageDownloader
/// {
///     public async Task&lt;ImageDownloadResult&gt; DownloadAsync(ImageRequest request, CancellationToken ct)
///     {
///         var msg = new HttpRequestMessage(HttpMethod.Get, request.Uri);
///         msg.Headers.Authorization = new("Bearer", await tokens.GetAsync(ct));
///
///         var response = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
///         response.EnsureSuccessStatusCode();
///
///         return new ImageDownloadResult(
///             await response.Content.ReadAsStreamAsync(ct),
///             response.Content.Headers.ContentLength
///         );
///     }
/// }
///
/// // builder.UseShinyControls(cfg =&gt; cfg.SetCustomImageDownloader&lt;AuthenticatedDownloader&gt;());
/// </code>
/// </example>
public interface IImageDownloader
{
    /// <summary>
    /// Fetch the image. Return the body stream unread - the caller reports progress while pumping it.
    /// Throw on any failure; the service turns that into a failed <see cref="ImageResult"/> and the
    /// control shows its error artwork.
    /// </summary>
    Task<ImageDownloadResult> DownloadAsync(ImageRequest request, CancellationToken cancellationToken);
}
