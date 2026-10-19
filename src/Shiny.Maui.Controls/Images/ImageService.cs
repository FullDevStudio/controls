using System.Collections.Concurrent;
using Shiny.Maui.Controls.Images.Caching;

namespace Shiny.Maui.Controls.Images;

/// <summary>
/// The default <see cref="IImageService"/>: memory cache, disk cache, a bounded download queue, and
/// de-duplication of concurrent requests for the same URI.
/// </summary>
/// <remarks>
/// <para>The disk tier follows the server. <c>Cache-Control: max-age</c> or <c>Expires</c> sets how long
/// an entry is trusted, falling back to <see cref="ImageOptions.DiskCacheDuration"/>. Once it expires
/// it is revalidated rather than thrown away: the request carries its <c>ETag</c> /
/// <c>Last-Modified</c>, and a 304 extends the entry without downloading the image again. If the
/// refresh fails outright, the stale copy is shown (<see cref="ImageOptions.ServeStaleOnError"/>).</para>
///
/// <para>Everything past the memory tier is shared per URI. Twelve cells asking for one avatar make
/// one disk read, or one request, and all twelve get its result.</para>
/// </remarks>
public class ImageService : IImageService
{
    /// <summary>
    /// One live download plus every caller waiting on it.
    /// </summary>
    /// <remarks>
    /// The whole reason this type exists is scrolling. Bind the same avatar URL into twelve visible
    /// cells and, without de-duplication, twelve requests go out for one image - each holding one of
    /// the four download slots, so the rest of the list stalls behind duplicates of a picture already
    /// being fetched. Here the first caller starts the download and the rest just attach a progress
    /// sink to it, so every one of them animates correctly off a single response.
    /// </remarks>
    sealed class InFlight
    {
        readonly List<IProgress<ImageLoadProgress>> subscribers = [];
        readonly Lock gate = new();

        public required Task<ImageResult> Task { get; init; }
        public ImageLoadProgress Latest { get; private set; } = ImageLoadProgress.Queued;

        public void Subscribe(IProgress<ImageLoadProgress>? progress)
        {
            if (progress is null)
                return;

            ImageLoadProgress snapshot;
            lock (this.gate)
            {
                this.subscribers.Add(progress);
                snapshot = this.Latest;
            }

            // A late joiner would otherwise sit at "None" until the next chunk arrives, which on a
            // nearly-finished download can be never.
            progress.Report(snapshot);
        }

        public void Unsubscribe(IProgress<ImageLoadProgress>? progress)
        {
            if (progress is null)
                return;

            lock (this.gate)
                this.subscribers.Remove(progress);
        }

        public void Report(ImageLoadProgress value)
        {
            IProgress<ImageLoadProgress>[] targets;
            lock (this.gate)
            {
                this.Latest = value;
                targets = [.. this.subscribers];
            }

            foreach (var target in targets)
                target.Report(value);
        }
    }

    // Reporting every chunk of a fast download floods the dispatcher with layout passes for changes
    // no eye can resolve. One percent or 100ms, whichever comes first, is smooth and cheap.
    static readonly TimeSpan MinReportInterval = TimeSpan.FromMilliseconds(100);
    const double MinReportDelta = 0.01;

    readonly ConcurrentDictionary<string, InFlight> inFlight = new(StringComparer.Ordinal);
    readonly SemaphoreSlim downloadGate;
    readonly IImageDownloader downloader;
    readonly TimeProvider time;

    /// <summary>Creates the service.</summary>
    /// <param name="options">Cache and concurrency settings. Defaults are used when null.</param>
    /// <param name="downloader">How bytes are fetched. A plain <see cref="HttpImageDownloader"/> when null.</param>
    /// <param name="time">The clock cache expiry and progress throttling run on. <see cref="TimeProvider.System"/> when null.</param>
    public ImageService(ImageOptions? options = null, IImageDownloader? downloader = null, TimeProvider? time = null)
    {
        this.Options = options ?? new ImageOptions();
        this.time = time ?? TimeProvider.System;
        this.downloader = downloader ?? new HttpImageDownloader(this.Options, null, this.time);
        this.DiskCache = new DiskImageCache(this.Options, this.time);
        this.MemoryCache = new MemoryImageCache(this.Options);
        this.downloadGate = new SemaphoreSlim(Math.Max(1, this.Options.MaxConcurrentDownloads));
    }


    /// <summary>The settings in force.</summary>
    public ImageOptions Options { get; }

    /// <summary>The on-disk tier.</summary>
    public DiskImageCache DiskCache { get; }

    /// <summary>The in-memory byte tier.</summary>
    public MemoryImageCache MemoryCache { get; }


    /// <inheritdoc />
    public async Task<ImageResult> GetAsync(
        ImageRequest request,
        IProgress<ImageLoadProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (String.IsNullOrWhiteSpace(request.Uri))
            return ImageResult.Fail(new ArgumentException("Image URI is empty", nameof(request)));

        try
        {
            // The memory tier is answered on the caller's own thread: it is a dictionary lookup, and
            // the overwhelmingly common case when a list scrolls back over images it has shown.
            if (UsesCache(request) && this.MemoryCache.Get(request.Uri) is { } cached)
                return ImageResult.Ok(ImageOrigin.Memory, cached, null, cached.LongLength);

            return await this.LoadSharedAsync(request, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ImageResult.Fail(ex);
        }
    }


    /// <inheritdoc />
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        this.MemoryCache.Clear();
        await this.DiskCache.ClearAsync(cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public async Task ClearCacheAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        this.MemoryCache.Remove(uri);
        await this.DiskCache.RemoveAsync(uri, cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public Task<long> GetCacheSizeAsync(CancellationToken cancellationToken = default)
        => this.DiskCache.GetSizeAsync(cancellationToken);


    /// <inheritdoc />
    public async Task PrefetchAsync(IEnumerable<string> uris, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);

        // Sequential on purpose. The download gate already caps real concurrency, and firing the
        // whole list at once would fill every slot with speculative work, starving the images the
        // user is actually looking at.
        foreach (var uri in uris)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (String.IsNullOrWhiteSpace(uri))
                continue;

            await this.GetAsync(new ImageRequest(uri), null, cancellationToken).ConfigureAwait(false);
        }
    }


    static bool UsesCache(ImageRequest request) => request.CacheEnabled && !request.BypassCache;


    /// <summary>
    /// Requests are only shared with others that want the same thing: a forced reload must not be
    /// handed a cache hit that a normal load is in the middle of reading, and an uncached request
    /// must not be handed bytes a cached one wrote.
    /// </summary>
    static string ShareKey(ImageRequest request)
        => (!request.CacheEnabled ? "n:" : request.BypassCache ? "b:" : "c:") + request.Uri;


    async Task<ImageResult> LoadSharedAsync(
        ImageRequest request,
        IProgress<ImageLoadProgress>? progress,
        CancellationToken cancellationToken
    )
    {
        var key = ShareKey(request);

        // The entry is created before the work starts and published with GetOrAdd's value overload,
        // so exactly one caller can win the race - and only that caller starts the download. Using
        // the factory overload instead would risk running it twice under contention and firing two
        // requests for the URL this whole path exists to fetch once.
        var tcs = new TaskCompletionSource<ImageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var candidate = new InFlight { Task = tcs.Task };

        var entry = this.inFlight.GetOrAdd(key, candidate);
        var isOwner = ReferenceEquals(entry, candidate);

        entry.Subscribe(progress);

        try
        {
            if (isOwner)
            {
                // The owner's own cancellation must not kill a download other callers are waiting
                // on, so the work runs against CancellationToken.None and only this caller's await
                // observes the token below.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await this.LoadCoreAsync(request, entry, CancellationToken.None)
                            .ConfigureAwait(false);
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetResult(ImageResult.Fail(ex));
                    }
                    finally
                    {
                        this.inFlight.TryRemove(new KeyValuePair<string, InFlight>(key, entry));
                    }
                }, CancellationToken.None);
            }

            return await entry.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            entry.Unsubscribe(progress);
        }
    }


    async Task<ImageResult> LoadCoreAsync(ImageRequest request, InFlight entry, CancellationToken cancellationToken)
    {
        CacheEntryMeta? stale = null;

        if (UsesCache(request))
        {
            // Checked again now this caller owns the load. Between its own memory miss and winning
            // the race, a load of the same URI can have finished and left the shared map - and
            // without this look the owner would download a picture that is already on disk.
            if (this.MemoryCache.Get(request.Uri) is { } cached)
                return ImageResult.Ok(ImageOrigin.Memory, cached, null, cached.LongLength);

            var meta = await this.DiskCache.GetEntryAsync(request.Uri, cancellationToken).ConfigureAwait(false);
            if (meta is not null && !meta.IsExpiredAt(this.time.GetUtcNow()))
                return await this.FromDiskAsync(request, meta, cancellationToken).ConfigureAwait(false);

            stale = meta;
        }

        try
        {
            return await this.DownloadCoreAsync(request, entry, stale, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (stale is not null && this.Options.ServeStaleOnError)
        {
            return await this.FromDiskAsync(request, stale, cancellationToken).ConfigureAwait(false);
        }
    }


    async Task<ImageResult> FromDiskAsync(ImageRequest request, CacheEntryMeta meta, CancellationToken cancellationToken)
    {
        var path = this.DiskCache.GetImagePath(request.Uri);
        var bytes = await this.TryHoldInMemoryAsync(request, path, meta.ContentLength, cancellationToken)
            .ConfigureAwait(false);

        return ImageResult.Ok(ImageOrigin.Disk, bytes, path, meta.ContentLength);
    }


    async Task<ImageResult> DownloadCoreAsync(
        ImageRequest request,
        InFlight entry,
        CacheEntryMeta? stale,
        CancellationToken cancellationToken
    )
    {
        // An expired copy with validators turns the download into a question - "has this changed?" -
        // that a 304 answers with headers alone.
        if (stale is { CanRevalidate: true })
            request = request with { IfNoneMatch = stale.ETag, IfModifiedSince = stale.LastModified };

        // Queued is reported before the wait, not after. That ordering is the feature: a caller
        // sitting behind three other downloads has nothing to measure, so its ring spins.
        entry.Report(ImageLoadProgress.Queued);
        await this.downloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var download = await this.downloader.DownloadAsync(request, cancellationToken).ConfigureAwait(false);
            await using var body = download.Stream;

            if (download.NotModified)
            {
                if (stale is null)
                    throw new InvalidOperationException($"'{request.Uri}' answered 304 Not Modified, but nothing is cached for it");

                var refreshed = await this.DiskCache
                    .RefreshAsync(request.Uri, stale, download, request.CacheDuration ?? this.Options.DiskCacheDuration, cancellationToken)
                    .ConfigureAwait(false);

                entry.Report(new ImageLoadProgress(ImageLoadState.Loaded, refreshed.ContentLength, refreshed.ContentLength));
                return await this.FromDiskAsync(request, refreshed, cancellationToken).ConfigureAwait(false);
            }

            var total = download.ContentLength is > 0 ? download.ContentLength : null;
            entry.Report(new ImageLoadProgress(ImageLoadState.Downloading, 0, total));

            var lastReport = this.time.GetTimestamp();
            var lastPercent = -1d;

            void Report(long read)
            {
                var now = this.time.GetTimestamp();
                var percent = total is > 0 ? (double)read / total.Value : -1;

                if (this.time.GetElapsedTime(lastReport, now) < MinReportInterval && (percent < 0 || percent - lastPercent < MinReportDelta))
                    return;

                lastReport = now;
                lastPercent = percent;
                entry.Report(new ImageLoadProgress(ImageLoadState.Downloading, read, total));
            }

            if (request.CacheEnabled)
            {
                var meta = await this.DiskCache
                    .WriteAsync(request.Uri, body, download, request.CacheDuration ?? this.Options.DiskCacheDuration, Report, cancellationToken)
                    .ConfigureAwait(false);

                var path = this.DiskCache.GetImagePath(request.Uri);
                var bytes = await this.TryHoldInMemoryAsync(request, path, meta.ContentLength, cancellationToken)
                    .ConfigureAwait(false);

                entry.Report(new ImageLoadProgress(ImageLoadState.Loaded, meta.ContentLength, meta.ContentLength));

                // Fire-and-forget: the image is ready and the user should see it now, not after a
                // directory walk. Failures are already swallowed inside the trim.
                _ = this.DiskCache.TrimAsync(CancellationToken.None);

                return ImageResult.Ok(ImageOrigin.Network, bytes, path, meta.ContentLength);
            }

            var buffered = await ReadAllAsync(body, Report, cancellationToken).ConfigureAwait(false);
            entry.Report(new ImageLoadProgress(ImageLoadState.Loaded, buffered.LongLength, buffered.LongLength));
            return ImageResult.Ok(ImageOrigin.Network, buffered, null, buffered.LongLength);
        }
        finally
        {
            this.downloadGate.Release();
        }
    }


    async Task<byte[]?> TryHoldInMemoryAsync(ImageRequest request, string path, long length, CancellationToken cancellationToken)
    {
        if (!request.CacheEnabled || !this.Options.MemoryCacheEnabled || length > this.Options.MaxMemoryItemBytes)
            return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            this.MemoryCache.Set(request.Uri, bytes);
            return bytes;
        }
        catch
        {
            // The disk entry is still valid; the caller falls back to loading from the file path.
            return null;
        }
    }


    static async Task<byte[]> ReadAllAsync(Stream source, Action<long> report, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        long read = 0;
        int count;

        while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            memory.Write(buffer, 0, count);
            read += count;
            report(read);
        }

        return memory.ToArray();
    }
}
