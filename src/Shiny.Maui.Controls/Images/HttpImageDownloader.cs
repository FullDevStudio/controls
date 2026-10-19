using System.Net.Http.Headers;

namespace Shiny.Maui.Controls.Images;

/// <summary>
/// The default <see cref="IImageDownloader"/> - a plain HTTP GET.
/// </summary>
/// <remarks>
/// Uses <see cref="HttpCompletionOption.ResponseHeadersRead"/> rather than the default. That is not
/// a micro-optimization: it is what makes the headers - above all <c>Content-Length</c> - available
/// before the body arrives, and the presence of that header is the entire difference between the
/// user seeing a ring that fills to a percentage and one that just spins.
/// </remarks>
public class HttpImageDownloader : IImageDownloader
{
    // Only built when the app registered no HttpClient of its own. Static because one client shared
    // across every image is the correct shape - a client per request exhausts sockets.
    static readonly Lazy<HttpClient> LazyFallbackClient = new(() =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ShinyControls/1.0");
        return client;
    });

    readonly HttpClient client;
    readonly ImageOptions options;
    readonly TimeProvider time;

    /// <summary>Creates the downloader.</summary>
    /// <param name="options">Cache and timeout settings.</param>
    /// <param name="client">
    /// The client to use. When null a shared internal one is used - which is the normal case, since
    /// an app that wanted its own client would be registering its own downloader.
    /// </param>
    /// <param name="time">The clock a <c>max-age</c> is counted from. <see cref="TimeProvider.System"/> when null.</param>
    public HttpImageDownloader(ImageOptions? options = null, HttpClient? client = null, TimeProvider? time = null)
    {
        this.options = options ?? new ImageOptions();
        this.client = client ?? LazyFallbackClient.Value;
        this.time = time ?? TimeProvider.System;
    }


    /// <inheritdoc />
    public virtual async Task<ImageDownloadResult> DownloadAsync(ImageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var timeoutCts = new CancellationTokenSource(this.options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var msg = new HttpRequestMessage(HttpMethod.Get, request.Uri);
        if (request.Headers is not null)
        {
            foreach (var pair in request.Headers)
                msg.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        }

        if (!String.IsNullOrEmpty(request.IfNoneMatch))
            msg.Headers.TryAddWithoutValidation("If-None-Match", request.IfNoneMatch);

        if (request.IfModifiedSince is { } since)
            msg.Headers.IfModifiedSince = since;

        var response = await this.client
            .SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, linked.Token)
            .ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
            // A 304 carries no body, but it may carry a fresh max-age - which is the whole point of
            // asking: the copy on disk is good for another stretch, for the price of the headers.
            using (response)
            {
                return new ImageDownloadResult(
                    Stream.Null,
                    ExpiresUtc: ResolveExpiry(response.Headers.CacheControl, response.Content.Headers.Expires, this.time.GetUtcNow()),
                    ETag: response.Headers.ETag?.Tag,
                    NotModified: true
                );
            }
        }

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);

        return new ImageDownloadResult(
            stream,
            response.Content.Headers.ContentLength,
            response.Content.Headers.ContentType?.MediaType,
            ResolveExpiry(response.Headers.CacheControl, response.Content.Headers.Expires, this.time.GetUtcNow()),
            response.Headers.ETag?.Tag,
            response.Content.Headers.LastModified
        );
    }


    /// <summary>
    /// Turns the response's caching headers into an absolute expiry, or null to fall back to the
    /// configured duration.
    /// </summary>
    /// <remarks>
    /// <c>no-store</c>/<c>no-cache</c> map to "already expired" rather than to null. Null would mean
    /// "you decide", and this code deciding to cache something the server explicitly asked it not to
    /// is the wrong answer. An expired entry is still written - it just always revalidates - which
    /// keeps the write path uniform and costs one file that gets trimmed away later.
    /// </remarks>
    internal static DateTimeOffset? ResolveExpiry(CacheControlHeaderValue? cacheControl, DateTimeOffset? expires, DateTimeOffset now)
    {
        if (cacheControl is not null)
        {
            if (cacheControl.NoStore || cacheControl.NoCache)
                return DateTimeOffset.MinValue;

            if (cacheControl.MaxAge is { } maxAge)
                return now.Add(maxAge);
        }
        return expires;
    }
}
